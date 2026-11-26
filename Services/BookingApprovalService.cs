using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public enum ApprovalOutcome { Approved, NotPending, CheckInPassed, DatesOverlap }

/// <summary>
/// Approves a pending booking request. Approval is "check the calendar, then take it", so two
/// approvals for the same property must never run that check at the same time: both would see a
/// free calendar and both would succeed. The property row is locked inside the transaction, which
/// makes concurrent approvals for one property run one after the other. The second one then sees
/// the first one's booking and is refused.
/// </summary>
public class BookingApprovalService(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<ApprovalOutcome> ApproveAsync(Booking booking)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        // A no-op update takes a row lock that is held until the transaction ends.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Properties SET IsActive = IsActive WHERE Id = {booking.PropertyId}");

        // The request may have been decided while we waited for the lock.
        await db.Entry(booking).ReloadAsync();

        if (booking.Status != BookingStatus.Pending) return ApprovalOutcome.NotPending;
        if (booking.CheckInDate.Date < clock.GetLocalNow().Date) return ApprovalOutcome.CheckInPassed;
        if (await db.Bookings.Overlapping(booking.PropertyId, booking.CheckInDate, booking.CheckOutDate).AnyAsync())
            return ApprovalOutcome.DatesOverlap;

        booking.Status = BookingStatus.Approved;
        if (booking.Payment is not null) booking.Payment.Status = PaymentStatus.Paid;
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return ApprovalOutcome.Approved;
    }
}
