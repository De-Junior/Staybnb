using Microsoft.EntityFrameworkCore;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Tests;

public class BookingApprovalServiceTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 11, 26);

    private readonly TestDb _t = new();
    private readonly BookingApprovalService _approvals;
    private readonly ApplicationUser _host;
    private readonly ApplicationUser _guest;
    private readonly HostProperty _property;

    public BookingApprovalServiceTests()
    {
        _approvals = new BookingApprovalService(_t.Db, new FixedClock(Today));
        _host = _t.AddUser("host@test.local");
        _guest = _t.AddUser("guest@test.local");
        _property = _t.AddProperty(_host);
    }

    public void Dispose() => _t.Dispose();

    private Booking Pending(string checkIn, string checkOut) =>
        _t.AddBooking(_property, _guest, DateTime.Parse(checkIn), DateTime.Parse(checkOut));

    [Fact]
    public async Task Approving_a_pending_request_approves_it_and_marks_it_paid()
    {
        var booking = Pending("2026-12-10", "2026-12-13");

        var outcome = await _approvals.ApproveAsync(booking);

        Assert.Equal(ApprovalOutcome.Approved, outcome);
        var saved = await _t.Db.Bookings.Include(b => b.Payment).AsNoTracking().SingleAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.Approved, saved.Status);
        Assert.Equal(PaymentStatus.Paid, saved.Payment!.Status);
    }

    [Fact]
    public async Task A_second_overlapping_request_is_refused_and_stays_pending()
    {
        var first = Pending("2026-12-10", "2026-12-13");
        var second = Pending("2026-12-12", "2026-12-15");

        Assert.Equal(ApprovalOutcome.Approved, await _approvals.ApproveAsync(first));
        Assert.Equal(ApprovalOutcome.DatesOverlap, await _approvals.ApproveAsync(second));

        var saved = await _t.Db.Bookings.AsNoTracking().SingleAsync(b => b.Id == second.Id);
        Assert.Equal(BookingStatus.Pending, saved.Status);
    }

    [Fact]
    public async Task Back_to_back_requests_can_both_be_approved()
    {
        var first = Pending("2026-12-10", "2026-12-13");
        var second = Pending("2026-12-13", "2026-12-15");

        Assert.Equal(ApprovalOutcome.Approved, await _approvals.ApproveAsync(first));
        Assert.Equal(ApprovalOutcome.Approved, await _approvals.ApproveAsync(second));
    }

    [Fact]
    public async Task Approving_the_same_request_twice_is_refused_the_second_time()
    {
        var booking = Pending("2026-12-10", "2026-12-13");

        Assert.Equal(ApprovalOutcome.Approved, await _approvals.ApproveAsync(booking));
        Assert.Equal(ApprovalOutcome.NotPending, await _approvals.ApproveAsync(booking));
    }

    [Fact]
    public async Task A_request_decided_elsewhere_is_not_approved_from_stale_data()
    {
        // The host's page still shows the request as pending, but it was rejected in another tab.
        var booking = Pending("2026-12-10", "2026-12-13");
        await _t.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Bookings SET Status = {(int)BookingStatus.Rejected} WHERE Id = {booking.Id}");

        Assert.Equal(ApprovalOutcome.NotPending, await _approvals.ApproveAsync(booking));
    }

    [Fact]
    public async Task A_request_whose_check_in_has_passed_is_refused()
    {
        var booking = Pending("2026-11-20", "2026-11-23");

        Assert.Equal(ApprovalOutcome.CheckInPassed, await _approvals.ApproveAsync(booking));
    }

    [Fact]
    public async Task A_request_checking_in_today_can_still_be_approved()
    {
        var booking = Pending("2026-11-26", "2026-11-28");

        Assert.Equal(ApprovalOutcome.Approved, await _approvals.ApproveAsync(booking));
    }
}
