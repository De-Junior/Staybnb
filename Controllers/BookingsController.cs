using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Guest)]
public class BookingsController(
    ApplicationDbContext db,
    BookingCalculator calculator,
    IActivityLogger activity,
    INotificationService notifications) : AppController
{
    private const int MaxNights = 90;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingRequestViewModel model)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.IsActive);
        if (property is null) return NotFound();

        var checkIn = model.CheckInDate.Date;
        var checkOut = model.CheckOutDate.Date;

        string? error = null;
        if (checkIn < DateTime.Today)
            error = "Check in date cannot be in the past.";
        else if (checkOut <= checkIn)
            error = "Checkout must be at least one night after check in.";
        else if ((checkOut - checkIn).Days > MaxNights)
            error = $"Stays are limited to {MaxNights} nights.";
        else if (model.NumberOfGuests < 1 || model.NumberOfGuests > property.MaxGuests)
            error = $"This place allows between 1 and {property.MaxGuests} guests.";
        else if (await db.Bookings.Overlapping(property.Id, checkIn, checkOut).AnyAsync())
            error = "Those dates are already booked. Please choose different dates.";
        else if (await db.Bookings.AnyAsync(b => b.GuestId == CurrentUserId && b.PropertyId == property.Id
                                                 && b.Status == BookingStatus.Pending
                                                 && b.CheckInDate < checkOut && b.CheckOutDate > checkIn))
            error = "You already have a pending request for these dates.";

        if (error is not null)
        {
            Error(error);
            return RedirectToAction("Details", "Properties", new
            {
                id = property.Id,
                checkIn = checkIn.ToString("yyyy-MM-dd"),
                checkOut = checkOut.ToString("yyyy-MM-dd"),
                guests = model.NumberOfGuests
            });
        }

        var quote = calculator.Quote(property.PricePerNight, property.CleaningFee, checkIn, checkOut);
        var booking = new Booking
        {
            PropertyId = property.Id,
            GuestId = CurrentUserId,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            NumberOfGuests = model.NumberOfGuests,
            PricePerNight = quote.PricePerNight,
            CleaningFee = quote.CleaningFee,
            ServiceFee = quote.ServiceFee,
            TotalPrice = quote.Total,
            Status = BookingStatus.Pending,
            Payment = new Payment { Amount = quote.Total, Status = PaymentStatus.Pending }
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        await activity.LogAsync(CurrentUserId, "BookingRequested", ActivityType.Booking,
            $"Booking #{booking.Id} for '{property.Title}' {checkIn:yyyy-MM-dd} to {checkOut:yyyy-MM-dd}, status Pending");

        await notifications.NotifyAsync(property.HostId, "New Booking Request",
            $"{User.Identity?.Name} requested '{property.Title}' from {checkIn:dd MMM} to {checkOut:dd MMM yyyy}.",
            NotificationType.Booking, "/Host/Bookings");

        Success("Your booking request was sent to the host. You'll be notified when they respond.");
        return RedirectToAction(nameof(Details), new { id = booking.Id });
    }

    public async Task<IActionResult> Index()
    {
        var bookings = await db.Bookings.AsNoTracking()
            .Include(b => b.Property).ThenInclude(p => p.Images)
            .Include(b => b.Payment)
            .Include(b => b.GuestCheckIn)
            .Where(b => b.GuestId == CurrentUserId)
            .OrderByDescending(b => b.CreatedAt)
            .AsSplitQuery()
            .ToListAsync();

        ViewBag.ReviewedPropertyIds = await db.Reviews
            .Where(r => r.ReviewerId == CurrentUserId)
            .Select(r => r.PropertyId)
            .ToListAsync();

        return View(bookings);
    }

    public async Task<IActionResult> Details(int id)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Include(b => b.Property).ThenInclude(p => p.Images)
            .Include(b => b.Property).ThenInclude(p => p.Host)
            .Include(b => b.Payment)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.Documents)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == CurrentUserId);

        if (booking is null) return NotFound();

        ViewBag.HasReviewed = await db.Reviews.AnyAsync(r => r.ReviewerId == CurrentUserId && r.PropertyId == booking.PropertyId);
        return View(booking);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await db.Bookings
            .Include(b => b.Property)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == CurrentUserId);

        if (booking is null) return NotFound();

        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Approved) || booking.CheckInDate.Date <= DateTime.Today)
        {
            Error("This booking can no longer be cancelled.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var previous = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        if (booking.Payment is not null)
            booking.Payment.Status = booking.Payment.Status == PaymentStatus.Paid ? PaymentStatus.Refunded : PaymentStatus.Failed;

        await db.SaveChangesAsync();

        await activity.LogAsync(CurrentUserId, "BookingCancelled", ActivityType.Booking,
            $"Booking #{booking.Id} for '{booking.Property.Title}': {previous} to Cancelled");

        await notifications.NotifyAsync(booking.Property.HostId, "Booking Cancelled",
            $"The booking for '{booking.Property.Title}' ({booking.CheckInDate:dd MMM} to {booking.CheckOutDate:dd MMM}) was cancelled by the guest.",
            NotificationType.Booking, "/Host/Bookings");

        Success("Your booking has been cancelled.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(ReviewFormViewModel model)
    {
        var booking = await db.Bookings
            .Include(b => b.Property)
            .FirstOrDefaultAsync(b => b.Id == model.BookingId && b.GuestId == CurrentUserId);

        if (booking is null) return NotFound();

        if (booking.Status is not (BookingStatus.CheckedIn or BookingStatus.Completed))
            Error("You can review a place once you have checked in.");
        else if (await db.Reviews.AnyAsync(r => r.ReviewerId == CurrentUserId && r.PropertyId == booking.PropertyId))
            Error("You have already reviewed this place.");
        else if (!ModelState.IsValid)
            Error("Please give a rating between 1 and 5 stars.");
        else
        {
            db.Reviews.Add(new Review
            {
                PropertyId = booking.PropertyId,
                ReviewerId = CurrentUserId,
                Rating = model.Rating,
                Comment = model.Comment?.Trim()
            });
            await db.SaveChangesAsync();

            await notifications.NotifyAsync(booking.Property.HostId, "New Review",
                $"'{booking.Property.Title}' received a {model.Rating}-star review.", NotificationType.Info, "/Host/Reviews");

            Success("Thanks for your review!");
        }

        return RedirectToAction(nameof(Details), new { id = model.BookingId });
    }
}
