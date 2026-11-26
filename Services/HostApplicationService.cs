using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Services;

public record ServiceResult(bool Succeeded, string Message)
{
    public static ServiceResult Ok(string message) => new(true, message);
    public static ServiceResult Fail(string message) => new(false, message);
}

public class HostApplicationService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IFileStorage storage,
    IActivityLogger activity,
    INotificationService notifications)
{
    // Caller must have validated the form, including that at least one image is present.
    public async Task<HostApplication> SubmitAsync(ApplicationUser applicant, PropertyFormViewModel form)
    {
        var isAdditional = await userManager.IsInRoleAsync(applicant, Roles.Host);

        var property = new HostProperty { HostId = applicant.Id, IsActive = false };
        form.ApplyTo(property);
        property.Amenities = await db.Amenities.Where(a => form.AmenityIds.Contains(a.Id)).ToListAsync();

        var order = 0;
        foreach (var file in form.Images)
            property.Images.Add(new PropertyImage { ImageUrl = await storage.SavePropertyImageAsync(file), DisplayOrder = order++ });

        var application = new HostApplication { ApplicationUserId = applicant.Id, Property = property };
        db.HostApplications.Add(application);
        await db.SaveChangesAsync();

        var action = isAdditional ? "AdditionalPropertyApplicationSubmitted" : "HostApplicationSubmitted";
        await activity.LogAsync(applicant.Id, action, ActivityType.HostApplication,
            $"Application #{application.Id} for '{property.Title}' ({property.City})");

        await notifications.NotifyAsync(applicant.Id,
            isAdditional ? "Additional Property Application Submitted" : "Host Application Submitted",
            $"Your application for '{property.Title}' has been received and is under review.",
            NotificationType.HostApplication, "/HostApplications/Status");

        await notifications.NotifyAdminsAsync("New Host Application",
            $"{applicant.FullName} submitted '{property.Title}' for review.",
            NotificationType.HostApplication, $"/Admin/ApplicationDetails/{application.Id}");

        return application;
    }

    public async Task<ServiceResult> ApproveAsync(int applicationId, string adminId, string? notes)
    {
        var application = await db.HostApplications
            .Include(a => a.ApplicationUser)
            .Include(a => a.Property).ThenInclude(p => p.CheckInProcess)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application is null) return ServiceResult.Fail("Application not found.");
        if (application.Status != ApplicationStatus.Pending) return ServiceResult.Fail("This application has already been reviewed.");

        var user = application.ApplicationUser;
        var wasGuest = await userManager.IsInRoleAsync(user, Roles.Guest);

        // UserManager shares this scoped DbContext, so the role swap and the approval commit or roll back together.
        await using var tx = await db.Database.BeginTransactionAsync();

        application.Status = ApplicationStatus.Approved;
        application.ReviewedAt = DateTime.UtcNow;
        application.AdminNotes = notes;
        application.Property.IsActive = true;
        application.Property.CheckInProcess ??= CheckInSteps.CreateDefaultProcess();
        await db.SaveChangesAsync();

        if (!await userManager.IsInRoleAsync(user, Roles.Host))
        {
            var added = await userManager.AddToRoleAsync(user, Roles.Host);
            if (!added.Succeeded) return ServiceResult.Fail(Describe("Could not add the Host role", added));
        }

        if (wasGuest)
        {
            var removed = await userManager.RemoveFromRoleAsync(user, Roles.Guest);
            if (!removed.Succeeded) return ServiceResult.Fail(Describe("Could not remove the Guest role", removed));
        }

        await tx.CommitAsync();

        await activity.LogAsync(adminId, "HostApplicationApproved", ActivityType.HostApplication,
            $"Application #{application.Id} for '{application.Property.Title}' by {user.Email}");

        if (wasGuest)
            await activity.LogAsync(adminId, "RoleChanged", ActivityType.RoleChange, $"{user.Email}: Guest to Host");

        await notifications.NotifyAsync(user.Id, "Host Application Approved",
            wasGuest
                ? $"Congratulations! Your host application for '{application.Property.Title}' has been approved. You are now a Host."
                : $"Your property '{application.Property.Title}' has been approved and is now live.",
            NotificationType.Success, "/Host/Dashboard");

        return ServiceResult.Ok(wasGuest
            ? $"Application approved. {user.FullName} is now a Host."
            : $"Application approved. '{application.Property.Title}' is now live.");
    }

    public async Task<ServiceResult> RejectAsync(int applicationId, string adminId, string? notes)
    {
        var application = await db.HostApplications
            .Include(a => a.ApplicationUser)
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application is null) return ServiceResult.Fail("Application not found.");
        if (application.Status != ApplicationStatus.Pending) return ServiceResult.Fail("This application has already been reviewed.");

        application.Status = ApplicationStatus.Rejected;
        application.ReviewedAt = DateTime.UtcNow;
        application.AdminNotes = notes;
        application.Property.IsActive = false;
        await db.SaveChangesAsync();

        await activity.LogAsync(adminId, "HostApplicationRejected", ActivityType.HostApplication,
            $"Application #{application.Id} for '{application.Property.Title}' by {application.ApplicationUser.Email}");

        await notifications.NotifyAsync(application.ApplicationUserId, "Host Application Rejected",
            $"Your application for '{application.Property.Title}' was not approved." +
            (string.IsNullOrWhiteSpace(notes) ? "" : $" Reason: {notes}"),
            NotificationType.Warning, "/HostApplications/Status");

        return ServiceResult.Ok("Application rejected.");
    }

    private static string Describe(string prefix, IdentityResult result) =>
        $"{prefix}: {string.Join(" ", result.Errors.Select(e => e.Description))}";
}
