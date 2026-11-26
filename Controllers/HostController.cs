using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Host)]
public class HostController(
    ApplicationDbContext db,
    IFileStorage storage,
    IActivityLogger activity,
    INotificationService notifications,
    BookingApprovalService approvals) : AppController
{
    private const int MaxImages = 10;

    private IQueryable<Booking> MyBookings => db.Bookings.Where(b => b.Property.HostId == CurrentUserId);

    // Host earnings exclude the platform's service fee
    private IQueryable<Booking> MyPaidBookings => MyBookings.Where(b => b.Payment != null && b.Payment.Status == PaymentStatus.Paid);

    public async Task<IActionResult> Dashboard()
    {
        var hostId = CurrentUserId;
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var reviews = db.Reviews.Where(r => r.Property.HostId == hostId);

        var model = new HostDashboardViewModel
        {
            HostName = (await db.Users.Where(u => u.Id == hostId).Select(u => u.FirstName + " " + u.LastName).FirstAsync()),
            TotalProperties = await db.Properties.CountAsync(p => p.HostId == hostId),
            ActiveProperties = await db.Properties.CountAsync(p => p.HostId == hostId && p.IsActive),
            TotalBookings = await MyBookings.CountAsync(),
            BookingsThisMonth = await MyBookings.CountAsync(b => b.CreatedAt >= monthStart),
            PendingRequests = await MyBookings.CountAsync(b => b.Status == BookingStatus.Pending),
            TotalEarnings = await MyPaidBookings.SumAsync(b => (decimal?)(b.TotalPrice - b.ServiceFee)) ?? 0,
            EarningsThisMonth = await MyPaidBookings.Where(b => b.CheckInDate >= monthStart)
                .SumAsync(b => (decimal?)(b.TotalPrice - b.ServiceFee)) ?? 0,
            AverageRating = await reviews.Select(r => (double?)r.Rating).AverageAsync() ?? 0,
            ReviewCount = await reviews.CountAsync(),
            CheckInsToVerify = await db.GuestCheckIns.CountAsync(c => c.Booking.Property.HostId == hostId && c.Status == CheckInStatus.Submitted),
            RecentRequests = await MyBookings.AsNoTracking()
                .Include(b => b.Guest).Include(b => b.Property)
                .Where(b => b.Status == BookingStatus.Pending)
                .OrderBy(b => b.CheckInDate).Take(5).ToListAsync(),
            UpcomingStays = await MyBookings.AsNoTracking()
                .Include(b => b.Guest).Include(b => b.Property).Include(b => b.GuestCheckIn)
                .Where(b => (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) && b.CheckOutDate >= today)
                .OrderBy(b => b.CheckInDate).Take(5).ToListAsync()
        };

        return View(model);
    }

    // ---------- Properties ----------

    public async Task<IActionResult> Properties()
    {
        var properties = await db.Properties.AsNoTracking()
            .Include(p => p.Images)
            .Where(p => p.HostId == CurrentUserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var ids = properties.Select(p => p.Id).ToList();

        var stats = await db.Properties
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                Bookings = p.Bookings.Count,
                Reviews = p.Reviews.Count,
                Rating = p.Reviews.Select(r => (double?)r.Rating).Average() ?? 0,
                LatestStatus = p.HostApplications.OrderByDescending(a => a.AppliedAt).Select(a => (ApplicationStatus?)a.Status).FirstOrDefault()
            })
            .ToDictionaryAsync(s => s.Id);

        var rows = properties.Select(p => new HostPropertyRowViewModel
        {
            Property = p,
            BookingCount = stats[p.Id].Bookings,
            ReviewCount = stats[p.Id].Reviews,
            AverageRating = stats[p.Id].Rating,
            LatestApplicationStatus = stats[p.Id].LatestStatus
        }).ToList();

        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == CurrentUserId);
        if (property is null) return NotFound();

        var approved = await db.HostApplications.AnyAsync(a => a.PropertyId == id && a.Status == ApplicationStatus.Approved);
        if (!approved)
        {
            Error("This property can only be activated once an admin has approved it.");
            return RedirectToAction(nameof(Properties));
        }

        property.IsActive = !property.IsActive;
        await db.SaveChangesAsync();

        await activity.LogAsync(CurrentUserId, property.IsActive ? "PropertyActivated" : "PropertyDeactivated",
            ActivityType.Property, $"Property #{property.Id} '{property.Title}'");

        Success(property.IsActive ? $"'{property.Title}' is now live." : $"'{property.Title}' is now hidden from guests.");
        return RedirectToAction(nameof(Properties));
    }

    [HttpGet]
    public async Task<IActionResult> EditProperty(int id)
    {
        var property = await db.Properties
            .Include(p => p.Images).Include(p => p.Amenities)
            .FirstOrDefaultAsync(p => p.Id == id && p.HostId == CurrentUserId);
        if (property is null) return NotFound();

        var model = PropertyFormViewModel.FromProperty(property);
        model.AvailableAmenities = await db.Amenities.OrderBy(a => a.Name).ToListAsync();
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProperty(int id, PropertyFormViewModel model)
    {
        var property = await db.Properties
            .Include(p => p.Images).Include(p => p.Amenities)
            .FirstOrDefaultAsync(p => p.Id == id && p.HostId == CurrentUserId);
        if (property is null) return NotFound();

        var remaining = property.Images.Count(i => !model.RemoveImageIds.Contains(i.Id)) + model.Images.Count;
        if (remaining == 0)
            ModelState.AddModelError(nameof(model.Images), "A property must keep at least one photo.");
        if (remaining > MaxImages)
            ModelState.AddModelError(nameof(model.Images), $"A property can have up to {MaxImages} photos.");

        foreach (var file in model.Images)
        {
            var error = storage.ValidateImage(file);
            if (error is not null) ModelState.AddModelError(nameof(model.Images), error);
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.ExistingImages = property.Images.OrderBy(i => i.DisplayOrder).ToList();
            model.AvailableAmenities = await db.Amenities.OrderBy(a => a.Name).ToListAsync();
            return View(model);
        }

        model.ApplyTo(property);

        property.Amenities.Clear();
        foreach (var amenity in await db.Amenities.Where(a => model.AmenityIds.Contains(a.Id)).ToListAsync())
            property.Amenities.Add(amenity);

        foreach (var image in property.Images.Where(i => model.RemoveImageIds.Contains(i.Id)).ToList())
        {
            storage.DeletePropertyImage(image.ImageUrl);
            db.PropertyImages.Remove(image);
        }

        var order = property.Images.Count == 0 ? 0 : property.Images.Max(i => i.DisplayOrder) + 1;
        foreach (var file in model.Images)
            property.Images.Add(new PropertyImage { ImageUrl = await storage.SavePropertyImageAsync(file), DisplayOrder = order++ });

        await db.SaveChangesAsync();
        await activity.LogAsync(CurrentUserId, "PropertyUpdated", ActivityType.Property, $"Property #{property.Id} '{property.Title}'");

        Success("Property updated.");
        return RedirectToAction(nameof(Properties));
    }

    // ---------- Booking requests ----------

    public async Task<IActionResult> Bookings(BookingStatus? status)
    {
        var query = MyBookings.AsNoTracking()
            .Include(b => b.Guest).Include(b => b.Property).Include(b => b.Payment).Include(b => b.GuestCheckIn)
            .AsQueryable();

        if (status.HasValue) query = query.Where(b => b.Status == status);

        ViewBag.Status = status;
        ViewBag.PendingCount = await MyBookings.CountAsync(b => b.Status == BookingStatus.Pending);

        return View(await query
            .OrderBy(b => b.Status == BookingStatus.Pending ? 0 : 1)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBooking(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking is null) return NotFound();

        switch (await approvals.ApproveAsync(booking))
        {
            case ApprovalOutcome.NotPending:
                Error("Only pending requests can be approved.");
                break;
            case ApprovalOutcome.CheckInPassed:
                Error("This request's check in date has passed. Reject it instead.");
                break;
            case ApprovalOutcome.DatesOverlap:
                Error("These dates overlap with a booking you have already approved.");
                break;
            default:
                await LogStatusChangeAsync(booking, BookingStatus.Pending);
                await notifications.NotifyAsync(booking.GuestId, "Booking Approved",
                    $"Your stay at '{booking.Property.Title}' ({booking.CheckInDate:dd MMM} to {booking.CheckOutDate:dd MMM}) is confirmed. Complete online check in before you arrive.",
                    NotificationType.Success, $"/Bookings/Details/{booking.Id}");

                Success($"Booking #{booking.Id} approved.");
                break;
        }

        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBooking(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking is null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            Error("Only pending requests can be rejected.");
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = BookingStatus.Rejected;
        if (booking.Payment is not null) booking.Payment.Status = PaymentStatus.Failed;
        await db.SaveChangesAsync();

        await LogStatusChangeAsync(booking, BookingStatus.Pending);
        await notifications.NotifyAsync(booking.GuestId, "Booking Declined",
            $"Unfortunately your request for '{booking.Property.Title}' was declined. You have not been charged.",
            NotificationType.Warning, $"/Bookings/Details/{booking.Id}");

        Success($"Booking #{booking.Id} rejected.");
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteBooking(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking is null) return NotFound();

        if (booking.Status != BookingStatus.CheckedIn)
        {
            Error("Only checked-in stays can be marked as completed.");
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = BookingStatus.Completed;
        await db.SaveChangesAsync();

        await LogStatusChangeAsync(booking, BookingStatus.CheckedIn);
        await notifications.NotifyAsync(booking.GuestId, "Stay Completed",
            $"Thanks for staying at '{booking.Property.Title}'. Tell others about your stay by leaving a review.",
            NotificationType.Booking, $"/Bookings/Details/{booking.Id}");

        Success($"Booking #{booking.Id} marked as completed.");
        return RedirectToAction(nameof(Bookings));
    }

    // ---------- Check in process ----------

    [HttpGet]
    public async Task<IActionResult> CheckInProcess(int propertyId)
    {
        var property = await db.Properties.AsNoTracking()
            .Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.HostId == CurrentUserId);
        if (property is null) return NotFound();

        var steps = CheckInSteps.Parse(property.CheckInProcess?.StepsJson);
        return View(new CheckInProcessFormViewModel
        {
            PropertyId = property.Id,
            PropertyTitle = property.Title,
            Title = property.CheckInProcess?.Title ?? "Standard check in",
            Steps = steps.Count > 0 ? steps : CheckInSteps.Defaults()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckInProcess(CheckInProcessFormViewModel model)
    {
        var property = await db.Properties
            .Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.HostId == CurrentUserId);
        if (property is null) return NotFound();

        model.Steps = model.Steps.Where(s => !string.IsNullOrWhiteSpace(s.Title)).ToList();
        foreach (var step in model.Steps)
        {
            step.Title = step.Title.Trim();
            step.DocumentType = step.RequiresDocument ? (string.IsNullOrWhiteSpace(step.DocumentType) ? "ID or Passport" : step.DocumentType.Trim()) : null;
        }

        if (model.Steps.Count == 0)
            ModelState.AddModelError(nameof(model.Steps), "Add at least one step.");
        else if (!model.Steps.Any(s => s.RequiresDocument))
            ModelState.AddModelError(nameof(model.Steps), "At least one step must ask the guest to upload a document such as an ID or passport.");

        if (!ModelState.IsValid)
        {
            model.PropertyTitle = property.Title;
            return View(model);
        }

        property.CheckInProcess ??= new CheckInProcess();
        property.CheckInProcess.Title = model.Title.Trim();
        property.CheckInProcess.StepsJson = CheckInSteps.Serialize(model.Steps);
        await db.SaveChangesAsync();

        await activity.LogAsync(CurrentUserId, "CheckInProcessUpdated", ActivityType.CheckIn,
            $"Property #{property.Id} '{property.Title}': {model.Steps.Count} steps");

        Success("Check in process saved.");
        return RedirectToAction(nameof(Properties));
    }

    // ---------- Guest check in verification ----------

    public async Task<IActionResult> CheckIns()
    {
        var list = await db.GuestCheckIns.AsNoTracking()
            .Include(c => c.Booking).ThenInclude(b => b.Guest)
            .Include(c => c.Booking).ThenInclude(b => b.Property)
            .Include(c => c.Documents)
            .Where(c => c.Booking.Property.HostId == CurrentUserId)
            .OrderBy(c => c.Status == CheckInStatus.Submitted ? 0 : 1)
            .ThenBy(c => c.Booking.CheckInDate)
            .AsSplitQuery()
            .ToListAsync();

        return View(list);
    }

    public async Task<IActionResult> CheckInReview(int id)
    {
        var checkIn = await db.GuestCheckIns.AsNoTracking()
            .Include(c => c.Booking).ThenInclude(b => b.Guest)
            .Include(c => c.Booking).ThenInclude(b => b.Property)
            .Include(c => c.CheckInProcess)
            .Include(c => c.Documents)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id && c.Booking.Property.HostId == CurrentUserId);

        return checkIn is null ? NotFound() : View(checkIn);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyDocument(int id, bool approve)
    {
        var doc = await db.GuestDocuments
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Documents)
            .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
            .FirstOrDefaultAsync(d => d.Id == id && d.GuestCheckIn.Booking.Property.HostId == CurrentUserId);
        if (doc is null) return NotFound();

        var checkIn = doc.GuestCheckIn;
        if (checkIn.Status != CheckInStatus.Submitted)
        {
            Error("Documents can only be reviewed after the guest submits check in.");
            return RedirectToAction(nameof(CheckInReview), new { id = checkIn.Id });
        }

        doc.Status = approve ? DocumentStatus.Verified : DocumentStatus.Rejected;
        var booking = checkIn.Booking;

        if (!approve)
        {
            checkIn.Status = CheckInStatus.Rejected;
            await db.SaveChangesAsync();

            await activity.LogAsync(CurrentUserId, "CheckInRejected", ActivityType.CheckIn,
                $"Booking #{booking.Id}: {doc.DocumentType} rejected");
            await notifications.NotifyAsync(booking.GuestId, "Check in Document Rejected",
                $"Your host could not verify your {doc.DocumentType} for '{booking.Property.Title}'. Please restart check in and upload a clearer copy.",
                NotificationType.Warning, $"/Bookings/Details/{booking.Id}");

            Success("Document rejected. The guest has been asked to upload it again.");
        }
        else if (checkIn.Documents.All(d => d.Status == DocumentStatus.Verified))
        {
            checkIn.Status = CheckInStatus.Verified;
            checkIn.CompletedAt = DateTime.UtcNow;
            booking.Status = BookingStatus.CheckedIn;
            await db.SaveChangesAsync();

            await activity.LogAsync(CurrentUserId, "CheckInVerified", ActivityType.CheckIn, $"Booking #{booking.Id}");
            await LogStatusChangeAsync(booking, BookingStatus.Approved);
            await notifications.NotifyAsync(booking.GuestId, "Check in Verified",
                $"You're checked in to '{booking.Property.Title}'. Enjoy your stay!",
                NotificationType.CheckIn, $"/Bookings/Details/{booking.Id}");

            Success("All documents verified. The guest is now checked in.");
        }
        else
        {
            await db.SaveChangesAsync();
            Success("Document verified.");
        }

        return RedirectToAction(nameof(CheckInReview), new { id = checkIn.Id });
    }

    // ---------- Earnings & reviews ----------

    public async Task<IActionResult> Earnings()
    {
        var paid = await MyPaidBookings.AsNoTracking()
            .Include(b => b.Property).Include(b => b.Guest).Include(b => b.Payment)
            .OrderByDescending(b => b.CheckInDate)
            .ToListAsync();

        var byMonth = paid
            .GroupBy(b => new DateTime(b.CheckInDate.Year, b.CheckInDate.Month, 1))
            .OrderByDescending(g => g.Key)
            .Select(g => (g.Key.ToString("MMMM yyyy", CultureInfo.InvariantCulture), g.Sum(b => b.TotalPrice - b.ServiceFee)))
            .ToList();

        return View(new EarningsViewModel
        {
            Total = paid.Sum(b => b.TotalPrice - b.ServiceFee),
            ByMonth = byMonth,
            Bookings = paid
        });
    }

    public async Task<IActionResult> Reviews()
    {
        var reviews = await db.Reviews.AsNoTracking()
            .Include(r => r.Reviewer).Include(r => r.Property)
            .Where(r => r.Property.HostId == CurrentUserId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(reviews);
    }

    private Task<Booking?> LoadBookingAsync(int id) =>
        db.Bookings.Include(b => b.Property).Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == id && b.Property.HostId == CurrentUserId);

    private Task LogStatusChangeAsync(Booking booking, BookingStatus from) =>
        activity.LogAsync(CurrentUserId, "BookingStatusChanged", ActivityType.Booking,
            $"Booking #{booking.Id} for '{booking.Property.Title}': {from} to {booking.Status}");
}
