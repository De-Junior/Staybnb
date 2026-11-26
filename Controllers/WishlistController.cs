using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Guest)]
public class WishlistController(ApplicationDbContext db) : AppController
{
    public async Task<IActionResult> Index()
    {
        var items = await db.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == CurrentUserId && w.Property.IsActive)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new PropertyCardViewModel
            {
                Id = w.Property.Id,
                Title = w.Property.Title,
                City = w.Property.City,
                Country = w.Property.Country,
                PropertyType = w.Property.PropertyType,
                PricePerNight = w.Property.PricePerNight,
                ImageUrls = w.Property.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
                AverageRating = w.Property.Reviews.Select(r => (double?)r.Rating).Average() ?? 0,
                ReviewCount = w.Property.Reviews.Count,
                IsWishlisted = true
            })
            .ToListAsync();

        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int propertyId, string? returnUrl)
    {
        var existing = await db.WishlistItems.FirstOrDefaultAsync(w => w.UserId == CurrentUserId && w.PropertyId == propertyId);

        if (existing is not null)
        {
            db.WishlistItems.Remove(existing);
        }
        else if (await db.Properties.AnyAsync(p => p.Id == propertyId && p.IsActive))
        {
            db.WishlistItems.Add(new WishlistItem { UserId = CurrentUserId, PropertyId = propertyId });
        }

        await db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }
}
