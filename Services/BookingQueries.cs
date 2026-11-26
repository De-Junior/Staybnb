using Staybnb.Models;

namespace Staybnb.Services;

public static class BookingQueries
{
    // Bookings that hold the property's calendar for any night in [checkIn, checkOut)
    public static IQueryable<Booking> Overlapping(this IQueryable<Booking> bookings, int propertyId, DateTime checkIn, DateTime checkOut) =>
        bookings.Where(b => b.PropertyId == propertyId
                            && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn)
                            && b.CheckInDate < checkOut
                            && b.CheckOutDate > checkIn);

    // Properties with no booking holding the calendar for any night in [checkIn, checkOut).
    // Same rule as Overlapping, written out so EF can translate it as one NOT EXISTS query.
    public static IQueryable<HostProperty> AvailableBetween(this IQueryable<HostProperty> properties, IQueryable<Booking> bookings, DateTime checkIn, DateTime checkOut) =>
        properties.Where(p => !bookings.Any(b => b.PropertyId == p.Id
                                                 && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn)
                                                 && b.CheckInDate < checkOut
                                                 && b.CheckOutDate > checkIn));
}
