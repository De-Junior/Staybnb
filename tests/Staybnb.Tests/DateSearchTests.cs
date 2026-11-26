using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Tests;

public class DateSearchTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly ApplicationUser _host;
    private readonly ApplicationUser _guest;
    private readonly HostProperty _booked;
    private readonly HostProperty _free;

    public DateSearchTests()
    {
        _host = _t.AddUser("host@test.local");
        _guest = _t.AddUser("guest@test.local");
        _booked = _t.AddProperty(_host);
        _free = _t.AddProperty(_host);
        // The first property is taken for the nights of 10, 11 and 12 December
        _t.AddBooking(_booked, _guest, new DateTime(2026, 12, 10), new DateTime(2026, 12, 13), BookingStatus.Approved);
    }

    public void Dispose() => _t.Dispose();

    private List<int> AvailableIds(string checkIn, string checkOut) =>
        _t.Db.Properties.AvailableBetween(_t.Db.Bookings, DateTime.Parse(checkIn), DateTime.Parse(checkOut))
            .Select(p => p.Id).OrderBy(id => id).ToList();

    [Fact]
    public void A_property_booked_for_the_dates_is_left_out()
    {
        Assert.Equal([_free.Id], AvailableIds("2026-12-11", "2026-12-14"));
    }

    [Fact]
    public void Everything_is_returned_when_the_dates_are_free()
    {
        Assert.Equal([_booked.Id, _free.Id], AvailableIds("2026-12-20", "2026-12-22"));
    }

    [Fact]
    public void Arriving_the_day_the_other_guest_leaves_counts_as_available()
    {
        Assert.Equal([_booked.Id, _free.Id], AvailableIds("2026-12-13", "2026-12-15"));
    }

    [Fact]
    public void A_pending_request_does_not_hide_a_property_from_search()
    {
        var other = _t.AddProperty(_host);
        _t.AddBooking(other, _guest, new DateTime(2026, 12, 10), new DateTime(2026, 12, 13), BookingStatus.Pending);

        Assert.Contains(other.Id, AvailableIds("2026-12-10", "2026-12-13"));
    }

    [Theory]
    [InlineData("2026-12-08", "2026-12-11")]
    [InlineData("2026-12-12", "2026-12-15")]
    [InlineData("2026-12-08", "2026-12-15")]
    [InlineData("2026-12-11", "2026-12-12")]
    [InlineData("2026-12-13", "2026-12-15")]
    [InlineData("2026-12-08", "2026-12-10")]
    [InlineData("2026-12-20", "2026-12-22")]
    public void Search_and_the_booking_engine_agree_on_what_is_free(string checkIn, string checkOut)
    {
        var searchSaysFree = AvailableIds(checkIn, checkOut).Contains(_booked.Id);
        var engineSaysFree = !_t.Db.Bookings.Overlapping(_booked.Id, DateTime.Parse(checkIn), DateTime.Parse(checkOut)).Any();

        Assert.Equal(engineSaysFree, searchSaysFree);
    }
}
