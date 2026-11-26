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
}
