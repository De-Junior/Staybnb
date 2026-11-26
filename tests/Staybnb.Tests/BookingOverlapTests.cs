using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Tests;

/// <summary>
/// A booking holds the calendar for the nights in [CheckIn, CheckOut). The night of the check-out
/// date is free, so one guest can leave on the day another arrives.
/// </summary>
public class BookingOverlapTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly ApplicationUser _host;
    private readonly ApplicationUser _guest;
    private readonly HostProperty _property;

    public BookingOverlapTests()
    {
        _host = _t.AddUser("host@test.local");
        _guest = _t.AddUser("guest@test.local");
        _property = _t.AddProperty(_host);
        // Existing approved booking: nights of 10, 11 and 12 December
        _t.AddBooking(_property, _guest, new DateTime(2026, 12, 10), new DateTime(2026, 12, 13), BookingStatus.Approved);
    }

    public void Dispose() => _t.Dispose();

    private bool Blocked(int propertyId, DateTime checkIn, DateTime checkOut) =>
        _t.Db.Bookings.Overlapping(propertyId, checkIn, checkOut).Any();

    [Theory]
    [InlineData("2026-12-08", "2026-12-11")] // starts before, ends inside
    [InlineData("2026-12-12", "2026-12-15")] // starts inside, ends after
    [InlineData("2026-12-08", "2026-12-15")] // covers the whole existing stay
    [InlineData("2026-12-11", "2026-12-12")] // inside the existing stay
    [InlineData("2026-12-10", "2026-12-13")] // exactly the same dates
    public void Overlapping_dates_are_blocked(string checkIn, string checkOut) =>
        Assert.True(Blocked(_property.Id, DateTime.Parse(checkIn), DateTime.Parse(checkOut)));

    [Theory]
    [InlineData("2026-12-13", "2026-12-15")] // arrives the day the other guest leaves
    [InlineData("2026-12-08", "2026-12-10")] // leaves the day the other guest arrives
    [InlineData("2026-12-20", "2026-12-22")] // well after
    [InlineData("2026-12-01", "2026-12-05")] // well before
    public void Back_to_back_and_separate_dates_are_free(string checkIn, string checkOut) =>
        Assert.False(Blocked(_property.Id, DateTime.Parse(checkIn), DateTime.Parse(checkOut)));

    [Fact]
    public void Another_property_is_not_affected()
    {
        var other = _t.AddProperty(_host);

        Assert.False(Blocked(other.Id, new DateTime(2026, 12, 10), new DateTime(2026, 12, 13)));
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Completed)]
    public void Only_approved_and_checked_in_bookings_hold_the_calendar(BookingStatus status)
    {
        var other = _t.AddProperty(_host);
        _t.AddBooking(other, _guest, new DateTime(2026, 12, 10), new DateTime(2026, 12, 13), status);

        Assert.False(Blocked(other.Id, new DateTime(2026, 12, 10), new DateTime(2026, 12, 13)));
    }

    [Fact]
    public void A_checked_in_booking_holds_the_calendar()
    {
        var other = _t.AddProperty(_host);
        _t.AddBooking(other, _guest, new DateTime(2026, 12, 10), new DateTime(2026, 12, 13), BookingStatus.CheckedIn);

        Assert.True(Blocked(other.Id, new DateTime(2026, 12, 11), new DateTime(2026, 12, 12)));
    }

    [Fact]
    public void The_database_refuses_a_booking_that_ends_before_it_starts()
    {
        _t.Db.Bookings.Add(new Booking
        {
            PropertyId = _property.Id, GuestId = _guest.Id,
            CheckInDate = new DateTime(2026, 12, 20), CheckOutDate = new DateTime(2026, 12, 19)
        });

        Assert.Throws<Microsoft.EntityFrameworkCore.DbUpdateException>(() => _t.Db.SaveChanges());
    }
}
