using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.AdminOrSuperAdmin)]
public class AdminController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    HostApplicationService applications,
    IActivityLogger activity,
    INotificationService notifications) : AppController
{
    private const int LogPageSize = 25;

    public async Task<IActionResult> Dashboard()
    {
        var paid = db.Bookings.Where(b => b.Payment != null && b.Payment.Status == PaymentStatus.Paid);

        return View(new AdminDashboardViewModel
        {
            TotalUsers = await db.Users.CountAsync(),
            PendingApplications = await db.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending),
            ActiveProperties = await db.Properties.CountAsync(p => p.IsActive),
            PlatformRevenue = await paid.SumAsync(b => (decimal?)b.ServiceFee) ?? 0,
            TotalBookings = await db.Bookings.CountAsync(),
            PendingBookings = await db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            ApprovedHosts = (await userManager.GetUsersInRoleAsync(Roles.Host)).Count,
            RecentActivity = await db.ActivityLogs.AsNoTracking().Include(a => a.User)
                .OrderByDescending(a => a.CreatedAt).Take(8).ToListAsync(),
            RecentApplications = await db.HostApplications.AsNoTracking()
                .Include(a => a.ApplicationUser).Include(a => a.Property)
                .OrderByDescending(a => a.AppliedAt).Take(5).ToListAsync(),
            RecentBookings = await db.Bookings.AsNoTracking()
                .Include(b => b.Guest).Include(b => b.Property).Include(b => b.Payment)
                .OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync()
        });
    }

    // ---------- Host applications ----------

    // Defaults to the pending queue; "all" shows every application
    public async Task<IActionResult> Applications(string? status)
    {
        ApplicationStatus? filter = status == "all" ? null
            : Enum.TryParse<ApplicationStatus>(status, out var parsed) ? parsed
            : ApplicationStatus.Pending;

        var query = db.HostApplications.AsNoTracking()
            .Include(a => a.ApplicationUser)
            .Include(a => a.Property).ThenInclude(p => p.Images)
            .AsQueryable();

        if (filter.HasValue) query = query.Where(a => a.Status == filter);

        ViewBag.Status = filter;
        return View(await query.OrderByDescending(a => a.AppliedAt).ToListAsync());
    }

    public async Task<IActionResult> ApplicationDetails(int id)
    {
        var application = await db.HostApplications.AsNoTracking()
            .Include(a => a.ApplicationUser)
            .Include(a => a.Property).ThenInclude(p => p.Images)
            .Include(a => a.Property).ThenInclude(p => p.Amenities)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application is null) return NotFound();

        ViewBag.ApplicantRoles = await userManager.GetRolesAsync(application.ApplicationUser);
        return View(application);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveApplication(int id, string? notes)
    {
        var result = await applications.ApproveAsync(id, CurrentUserId, Clean(notes));
        if (result.Succeeded) Success(result.Message); else Error(result.Message);
        return RedirectToAction(nameof(ApplicationDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectApplication(int id, string? notes)
    {
        var result = await applications.RejectAsync(id, CurrentUserId, Clean(notes));
        if (result.Succeeded) Success(result.Message); else Error(result.Message);
        return RedirectToAction(nameof(ApplicationDetails), new { id });
    }

    // ---------- Users ----------

    public async Task<IActionResult> Users(string? search, string? role)
    {
        IQueryable<ApplicationUser> query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => u.Email!.Contains(term) || u.FirstName.Contains(term) || u.LastName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleId = await db.Roles.Where(r => r.Name == role).Select(r => r.Id).FirstOrDefaultAsync();
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId));
        }

        var users = await query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).Take(200).ToListAsync();
        var userIds = users.Select(u => u.Id).ToList();

        var roleLookup = await (from ur in db.UserRoles
                                join r in db.Roles on ur.RoleId equals r.Id
                                where userIds.Contains(ur.UserId)
                                select new { ur.UserId, r.Name })
            .ToListAsync();

        return View(new AdminUsersViewModel
        {
            Search = search,
            Role = role,
            Users = users.Select(u => new UserRowViewModel
            {
                User = u,
                Roles = roleLookup.Where(x => x.UserId == u.Id).Select(x => x.Name!).ToList()
            }).ToList()
        });
    }

    // Only the SuperAdmin may promote a Guest to Admin
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> PromoteToAdmin(string id, string? search)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (!await userManager.IsInRoleAsync(user, Roles.Guest))
        {
            Error("Only Guests can be promoted to Admin.");
            return RedirectToAction(nameof(Users), new { search });
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var added = await userManager.AddToRoleAsync(user, Roles.Admin);
        var removed = added.Succeeded ? await userManager.RemoveFromRoleAsync(user, Roles.Guest) : IdentityResult.Failed();

        if (!added.Succeeded || !removed.Succeeded)
        {
            Error("Could not update this user's roles.");
            return RedirectToAction(nameof(Users), new { search });
        }

        await tx.CommitAsync();

        await activity.LogAsync(CurrentUserId, "RoleChanged", ActivityType.RoleChange, $"{user.Email}: Guest to Admin");
        await notifications.NotifyAsync(user.Id, "You are now an Admin",
            "The SuperAdmin has given you administrator access to Staybnb.", NotificationType.Success, "/Admin/Dashboard");

        Success($"{user.FullName} is now an Admin.");
        return RedirectToAction(nameof(Users), new { search });
    }

    // ---------- Oversight ----------

    public async Task<IActionResult> ActivityLogs(ActivityType? type, string? search, int page = 1)
    {
        var query = db.ActivityLogs.AsNoTracking().Include(a => a.User).AsQueryable();

        if (type.HasValue) query = query.Where(a => a.ActivityType == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.Action.Contains(term)
                                     || (a.Details != null && a.Details.Contains(term))
                                     || (a.User != null && a.User.Email!.Contains(term)));
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)LogPageSize));
        page = Math.Clamp(page, 1, totalPages);

        return View(new ActivityLogsViewModel
        {
            Logs = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * LogPageSize).Take(LogPageSize).ToListAsync(),
            Type = type,
            Search = search,
            Page = page,
            TotalPages = totalPages,
            TotalCount = total
        });
    }

    public async Task<IActionResult> Bookings(BookingStatus? status)
    {
        var query = db.Bookings.AsNoTracking()
            .Include(b => b.Guest).Include(b => b.Property).Include(b => b.Payment)
            .AsQueryable();

        if (status.HasValue) query = query.Where(b => b.Status == status);

        ViewBag.Status = status;
        return View(await query.OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync());
    }

    public async Task<IActionResult> Properties()
    {
        var properties = await db.Properties.AsNoTracking()
            .Include(p => p.Host).Include(p => p.Images)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(properties);
    }

    private static string? Clean(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 500)];
}
