using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

public class HomeController(ApplicationDbContext db) : AppController
{
    private const int PageSize = 12;

    public async Task<IActionResult> Index(string? city, PropertyType? type, int? guests, int page = 1)
    {
        var query = db.Properties.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(p => p.City.Contains(city.Trim()));
        if (type.HasValue)
            query = query.Where(p => p.PropertyType == type);
        if (guests is > 0)
            query = query.Where(p => p.MaxGuests >= guests);

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var properties = await query
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new PropertyCardViewModel
            {
                Id = p.Id,
                Title = p.Title,
                City = p.City,
                Country = p.Country,
                PropertyType = p.PropertyType,
                PricePerNight = p.PricePerNight,
                ImageUrls = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList(),
                AverageRating = p.Reviews.Select(r => (double?)r.Rating).Average() ?? 0,
                ReviewCount = p.Reviews.Count
            })
            .ToListAsync();

        var canWishlist = User.IsInRole(Roles.Guest);
        if (canWishlist)
        {
            var wishlisted = await db.WishlistItems
                .Where(w => w.UserId == CurrentUserId)
                .Select(w => w.PropertyId)
                .ToListAsync();
            foreach (var card in properties)
                card.IsWishlisted = wishlisted.Contains(card.Id);
        }

        return View(new HomeIndexViewModel
        {
            Properties = properties,
            Cities = await db.Properties.Where(p => p.IsActive).Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync(),
            City = city,
            Type = type,
            Guests = guests,
            CanWishlist = canWishlist,
            Page = page,
            TotalPages = totalPages,
            TotalCount = totalCount
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(model: Activity.Current?.Id ?? HttpContext.TraceIdentifier);
}
