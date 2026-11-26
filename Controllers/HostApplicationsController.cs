using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Guests use this to "Become a Host"; existing Hosts use it to list an additional property.
[Authorize(Roles = Roles.Guest + "," + Roles.Host)]
public class HostApplicationsController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IFileStorage storage,
    HostApplicationService applications) : AppController
{
    private const int MaxImages = 10;

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (User.IsInRole(Roles.Guest) && await HasPendingApplicationAsync())
        {
            Error("You already have an application under review. We'll notify you once an admin has reviewed it.");
            return RedirectToAction(nameof(Status));
        }

        return View(new PropertyFormViewModel { AvailableAmenities = await db.Amenities.OrderBy(a => a.Name).ToListAsync() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PropertyFormViewModel model)
    {
        if (User.IsInRole(Roles.Guest) && await HasPendingApplicationAsync())
        {
            Error("You already have an application under review.");
            return RedirectToAction(nameof(Status));
        }

        // An application is incomplete without at least one image
        if (model.Images.Count == 0)
            ModelState.AddModelError(nameof(model.Images), "Add at least one photo of your property. Applications without an image cannot be submitted.");
        else if (model.Images.Count > MaxImages)
            ModelState.AddModelError(nameof(model.Images), $"You can upload up to {MaxImages} photos.");

        foreach (var file in model.Images)
        {
            var error = storage.ValidateImage(file);
            if (error is not null) ModelState.AddModelError(nameof(model.Images), error);
        }

        if (!ModelState.IsValid)
        {
            model.AvailableAmenities = await db.Amenities.OrderBy(a => a.Name).ToListAsync();
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        await applications.SubmitAsync(user!, model);

        Success("Your application has been submitted. An admin will review it shortly.");
        return User.IsInRole(Roles.Host)
            ? RedirectToAction("Properties", "Host")
            : RedirectToAction(nameof(Status));
    }

    public async Task<IActionResult> Status()
    {
        var list = await db.HostApplications.AsNoTracking()
            .Include(a => a.Property).ThenInclude(p => p.Images)
            .Where(a => a.ApplicationUserId == CurrentUserId)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync();

        return View(list);
    }

    private Task<bool> HasPendingApplicationAsync() =>
        db.HostApplications.AnyAsync(a => a.ApplicationUserId == CurrentUserId && a.Status == ApplicationStatus.Pending);
}
