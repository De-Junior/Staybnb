using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = Roles.Guest)]
public class CheckInController(
    ApplicationDbContext db,
    IFileStorage storage,
    IActivityLogger activity,
    INotificationService notifications) : AppController
{
    private const string DefaultDocumentType = "ID or Passport";

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int bookingId)
    {
        var booking = await db.Bookings
            .Include(b => b.Property).ThenInclude(p => p.CheckInProcess)
            .Include(b => b.GuestCheckIn)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestId == CurrentUserId);

        if (booking is null) return NotFound();

        if (booking.Status != BookingStatus.Approved)
        {
            Error("Online check in opens once the host has approved your booking.");
            return RedirectToAction("Details", "Bookings", new { id = bookingId });
        }

        var checkIn = booking.GuestCheckIn;
        if (checkIn is null)
        {
            var process = booking.Property.CheckInProcess ??= CheckInSteps.CreateDefaultProcess();
            checkIn = new GuestCheckIn { Booking = booking, CheckInProcess = process, Status = CheckInStatus.InProgress };
            db.GuestCheckIns.Add(checkIn);
            await db.SaveChangesAsync();

            await activity.LogAsync(CurrentUserId, "CheckInStarted", ActivityType.CheckIn,
                $"Booking #{booking.Id} at '{booking.Property.Title}'");
        }
        else if (checkIn.Status is CheckInStatus.NotStarted or CheckInStatus.Rejected)
        {
            checkIn.Status = CheckInStatus.InProgress;
            checkIn.CurrentStep = 0;
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Step), new { id = checkIn.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Step(int id)
    {
        var checkIn = await LoadAsync(id);
        if (checkIn is null) return NotFound();

        if (checkIn.Status != CheckInStatus.InProgress)
            return RedirectToAction("Details", "Bookings", new { id = checkIn.BookingId });

        return View(BuildStepModel(checkIn));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Step(int id, string direction, bool acknowledged, IFormFile? document)
    {
        var checkIn = await LoadAsync(id);
        if (checkIn is null) return NotFound();
        if (checkIn.Status != CheckInStatus.InProgress)
            return RedirectToAction("Details", "Bookings", new { id = checkIn.BookingId });

        var model = BuildStepModel(checkIn);

        if (direction == "back")
        {
            checkIn.CurrentStep = Math.Max(0, model.StepIndex - 1);
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Step), new { id });
        }

        var step = model.Step;
        if (step.RequiresDocument)
        {
            var documentType = step.DocumentType ?? DefaultDocumentType;

            if (document is not null)
            {
                var error = storage.ValidateDocument(document);
                if (error is not null)
                {
                    Error(error);
                    return RedirectToAction(nameof(Step), new { id });
                }

                // Replace any earlier upload of this document type (e.g. one the host rejected)
                foreach (var old in checkIn.Documents.Where(d => d.DocumentType == documentType).ToList())
                {
                    storage.DeleteGuestDocument(old.FileName);
                    db.GuestDocuments.Remove(old);
                }

                db.GuestDocuments.Add(new GuestDocument
                {
                    GuestCheckInId = checkIn.Id,
                    DocumentType = documentType,
                    FileName = await storage.SaveGuestDocumentAsync(document)
                });
            }
            else if (model.ExistingDocument is null)
            {
                Error($"Please upload your {documentType} to continue.");
                return RedirectToAction(nameof(Step), new { id });
            }
        }
        else if (!acknowledged)
        {
            Error("Please confirm that you have read this step.");
            return RedirectToAction(nameof(Step), new { id });
        }

        if (!model.IsLastStep)
        {
            checkIn.CurrentStep = model.StepIndex + 1;
            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Step), new { id });
        }

        checkIn.Status = CheckInStatus.Submitted;
        await db.SaveChangesAsync();

        var property = checkIn.Booking.Property;
        await activity.LogAsync(CurrentUserId, "CheckInSubmitted", ActivityType.CheckIn,
            $"Booking #{checkIn.BookingId} at '{property.Title}', documents awaiting host verification");

        await notifications.NotifyAsync(property.HostId, "Guest Check in Submitted",
            $"A guest completed online check in for '{property.Title}'. Please verify their documents.",
            NotificationType.CheckIn, $"/Host/CheckInReview/{checkIn.Id}");

        Success("Check in submitted. Your host will verify your documents before arrival.");
        return RedirectToAction("Details", "Bookings", new { id = checkIn.BookingId });
    }

    private Task<GuestCheckIn?> LoadAsync(int id) =>
        db.GuestCheckIns
            .Include(c => c.Booking).ThenInclude(b => b.Property).ThenInclude(p => p.Images)
            .Include(c => c.CheckInProcess)
            .Include(c => c.Documents)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id && c.Booking.GuestId == CurrentUserId);

    private static CheckInStepViewModel BuildStepModel(GuestCheckIn checkIn)
    {
        var steps = CheckInSteps.Parse(checkIn.CheckInProcess.StepsJson);
        if (steps.Count == 0) steps = CheckInSteps.Defaults();

        // Clamp in case the host shortened the process after the guest started
        var index = Math.Clamp(checkIn.CurrentStep, 0, steps.Count - 1);
        var step = steps[index];

        GuestDocument? existing = null;
        if (step.RequiresDocument)
        {
            var type = step.DocumentType ?? DefaultDocumentType;
            existing = checkIn.Documents
                .Where(d => d.DocumentType == type && d.Status != DocumentStatus.Rejected)
                .OrderByDescending(d => d.UploadedAt)
                .FirstOrDefault();
        }

        return new CheckInStepViewModel { CheckIn = checkIn, Steps = steps, StepIndex = index, ExistingDocument = existing };
    }
}
