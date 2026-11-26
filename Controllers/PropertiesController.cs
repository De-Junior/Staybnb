using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

public class PropertiesController(ApplicationDbContext db, BookingCalculator calculator) : AppController
{
    public async Task<IActionResult> Details(int id, DateTime? checkIn, DateTime? checkOut, int? guests)
    {
        var property = await db.Properties.AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Amenities)
            .Include(p => p.Host)
            .Include(p => p.Reviews).ThenInclude(r => r.Reviewer)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property is null) return NotFound();

        var userId = User.Identity?.IsAuthenticated == true ? CurrentUserId : null;
        var isOwner = userId == property.HostId;
        var isAdmin = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.SuperAdmin);

        // Inactive or not-yet-approved listings are only visible to their host and admins
        if (!property.IsActive && !isOwner && !isAdmin) return NotFound();

        var today = DateTime.Today;
        var bookedRanges = await db.Bookings
            .Where(b => b.PropertyId == id && b.CheckOutDate > today &&
                        (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn))
            .OrderBy(b => b.CheckInDate)
            .Select(b => new { b.CheckInDate, b.CheckOutDate })
            .ToListAsync();

        var isGuest = User.IsInRole(Roles.Guest);

        return View(new PropertyDetailsViewModel
        {
            Property = property,
            AverageRating = property.Reviews.Count > 0 ? property.Reviews.Average(r => r.Rating) : 0,
            CanBook = isGuest && property.IsActive,
            IsOwner = isOwner,
            IsWishlisted = isGuest && await db.WishlistItems.AnyAsync(w => w.UserId == userId && w.PropertyId == id),
            ServiceFeeRate = calculator.ServiceFeeRate,
            BookedRanges = bookedRanges.Select(b => (b.CheckInDate, b.CheckOutDate)).ToList(),
            Booking = new BookingRequestViewModel
            {
                PropertyId = id,
                CheckInDate = checkIn ?? today.AddDays(1),
                CheckOutDate = checkOut ?? today.AddDays(2),
                NumberOfGuests = guests ?? 1
            }
        });
    }
}
