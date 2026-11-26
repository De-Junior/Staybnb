using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Tests;

/// <summary>
/// A real (in-memory SQLite) database with the app's own model, so the queries and constraints
/// the tests exercise are the ones the app runs. One database per test class instance.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApplicationDbContext Db { get; }

    public TestDb()
    {
        _connection.Open();
        Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();
    }

    public ApplicationUser AddUser(string email, string firstName = "Test")
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, NormalizedUserName = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant(), FirstName = firstName, LastName = "User"
        };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    public HostProperty AddProperty(ApplicationUser host, decimal pricePerNight = 1000m, decimal cleaningFee = 200m, bool active = true)
    {
        var property = new HostProperty
        {
            Title = "Test place", Description = "A place", Address = "1 Test Street", City = "Giyani",
            PricePerNight = pricePerNight, CleaningFee = cleaningFee, MaxGuests = 4, HostId = host.Id, IsActive = active
        };
        Db.Properties.Add(property);
        Db.SaveChanges();
        return property;
    }

    public Booking AddBooking(HostProperty property, ApplicationUser guest, DateTime checkIn, DateTime checkOut,
        BookingStatus status = BookingStatus.Pending, int guests = 2)
    {
        var booking = new Booking
        {
            PropertyId = property.Id, GuestId = guest.Id, CheckInDate = checkIn.Date, CheckOutDate = checkOut.Date,
            NumberOfGuests = guests, PricePerNight = property.PricePerNight, CleaningFee = property.CleaningFee,
            Status = status, Payment = new Payment { Amount = 0, Status = PaymentStatus.Pending }
        };
        Db.Bookings.Add(booking);
        Db.SaveChanges();
        return booking;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

/// <summary>A clock the test controls.</summary>
public sealed class FixedClock(DateTime today) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(today.Date, TimeSpan.Zero);
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

public sealed class NoopActivityLogger : IActivityLogger
{
    public Task LogAsync(string? userId, string action, ActivityType type, string? details = null) => Task.CompletedTask;
}

public sealed class NoopNotifications : INotificationService
{
    public Task NotifyAsync(string userId, string title, string? body, NotificationType type, string? linkUrl = null) => Task.CompletedTask;
    public Task NotifyAdminsAsync(string title, string? body, NotificationType type, string? linkUrl = null) => Task.CompletedTask;
}

public sealed class NoopFileStorage : IFileStorage
{
    public string? ValidateImage(IFormFile file) => null;
    public string? ValidateDocument(IFormFile file) => null;
    public Task<string> SavePropertyImageAsync(IFormFile file) => Task.FromResult("/uploads/properties/test.jpg");
    public void DeletePropertyImage(string imageUrl) { }
    public Task<string> SaveGuestDocumentAsync(IFormFile file) => Task.FromResult("test.png");
    public string GetGuestDocumentPath(string fileName) => fileName;
    public string GetContentType(string fileName) => "image/png";
    public void DeleteGuestDocument(string fileName) { }
}
