using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Data;

public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var config = services.GetRequiredService<IConfiguration>();
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.MigrateAsync();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var sa = config.GetSection("Seed:SuperAdmin");
        var (superAdmin, created) = await EnsureUserAsync(userManager,
            sa["Email"]!, RequiredSecret(config, "Seed:SuperAdmin:Password"), sa["FirstName"]!, sa["LastName"]!, Roles.SuperAdmin);

        if (created)
        {
            db.ActivityLogs.Add(new ActivityLog
            {
                UserId = superAdmin.Id,
                Action = "SuperAdminSeeded",
                ActivityType = ActivityType.System,
                Details = $"SuperAdmin account {superAdmin.Email} created on startup"
            });
            await db.SaveChangesAsync();
        }

        await SeedAmenitiesAsync(db);

        if (config.GetValue<bool>("Seed:DemoData"))
        {
            var listingsFile = Path.Combine(env.ContentRootPath, "Data", "Seed", "demo-listings.json");
            await SeedDemoDataAsync(db, userManager, RequiredSecret(config, "Seed:DemoPassword"),
                config.GetValue("Booking:ServiceFeeRate", 0.12m), listingsFile);
        }
    }

    // Passwords are never kept in appsettings.json, which is committed. They come
    // from .NET user secrets in development or environment variables when hosted
    // (Seed__SuperAdmin__Password, Seed__DemoPassword). See README.md.
    private static string RequiredSecret(IConfiguration config, string key)
    {
        var value = config[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"'{key}' is not set. Run: dotnet user-secrets set \"{key}\" \"<password>\" (see README.md).");
        return value;
    }

    private static async Task<(ApplicationUser User, bool Created)> EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string password, string firstName, string lastName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null) return (user, false);

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not seed {email}: {string.Join(" ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(user, role);
        return (user, true);
    }

    private static async Task SeedAmenitiesAsync(ApplicationDbContext db)
    {
        if (await db.Amenities.AnyAsync()) return;

        db.Amenities.AddRange(
            new Amenity { Name = "Wifi", IconClass = "bi-wifi" },
            new Amenity { Name = "Kitchen", IconClass = "bi-cup-hot" },
            new Amenity { Name = "Washer", IconClass = "bi-droplet" },
            new Amenity { Name = "Dryer", IconClass = "bi-wind" },
            new Amenity { Name = "Air conditioning", IconClass = "bi-snow" },
            new Amenity { Name = "Heating", IconClass = "bi-thermometer-sun" },
            new Amenity { Name = "Pool", IconClass = "bi-water" },
            new Amenity { Name = "Free parking", IconClass = "bi-p-circle" },
            new Amenity { Name = "TV", IconClass = "bi-tv" },
            new Amenity { Name = "Dedicated workspace", IconClass = "bi-laptop" },
            new Amenity { Name = "Braai area", IconClass = "bi-fire" },
            new Amenity { Name = "Security", IconClass = "bi-shield-check" });

        await db.SaveChangesAsync();
    }

    // ---------- Demo data ----------

    private sealed record SeedListing(
        string Key, string Host, string Title, string Address, string City, PropertyType Type,
        decimal Price, decimal Cleaning, int Guests, int Bedrooms, int Beds, int Baths,
        string Description, string[] Amenities, int[] Photos, bool Pending = false);

    private static readonly (string Email, string First, string Last)[] Hosts =
    [
        ("host@staybnb.co.za", "Lerato", "Mokoena"),
        ("host2@staybnb.co.za", "Bongani", "Mthembu"),
        ("host3@staybnb.co.za", "Zanele", "Ndlovu"),
        ("host4@staybnb.co.za", "Pieter", "van Wyk"),
        ("host5@staybnb.co.za", "Ayesha", "Patel")
    ];

    private static readonly (string First, string Last)[] Reviewers =
    [
        ("Kagiso", "Molefe"), ("Thandeka", "Zulu"), ("Johan", "Botha"), ("Nomsa", "Khumalo"),
        ("Ryan", "Naidoo"), ("Lindiwe", "Mahlangu"), ("Michael", "Smith"), ("Palesa", "Sithole"),
        ("Tshepo", "Maseko"), ("Aisha", "Adams"), ("Sibusiso", "Ntuli"), ("Megan", "Jacobs")
    ];

    private static readonly string[] GreatComments =
    [
        "Absolutely loved our stay. The place was spotless and exactly like the photos.",
        "{host} was a fantastic host and replied to every message within minutes. Highly recommended!",
        "Beautiful space in a great location. We will definitely be back.",
        "Everything we needed for a relaxing weekend. The beds were super comfortable.",
        "Check in was easy and the place felt like home from the moment we arrived.",
        "One of the best stays we have had in {city}. Great value for money.",
        "Lovely, clean and safe. The little welcome touches made a big difference.",
        "Perfect for our family trip. The kids did not want to leave!",
        "Stylish, comfortable and very well equipped. Thank you {host}!",
        "The photos do not do it justice. Stunning place and a wonderful host."
    ];

    private static readonly string[] GoodComments =
    [
        "Nice place and a good location. The wifi was a bit slow but everything else was great.",
        "Comfortable stay overall. Parking was a little tight but {host} helped us sort it out.",
        "Good value for {city}. Would stay again.",
        "Clean and quiet. A few small things could be updated but we enjoyed our time."
    ];

    private static readonly string[] OkComments =
    [
        "The location was convenient, but the place needed a bit more cleaning when we arrived.",
        "Decent stay. Some appliances did not work, although {host} responded quickly."
    ];

    private static async Task SeedDemoDataAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        string password, decimal serviceFeeRate, string listingsFile)
    {
        if (await db.Properties.AnyAsync() || !File.Exists(listingsFile)) return;

        var (admin, _) = await EnsureUserAsync(userManager, "admin@staybnb.co.za", password, "Ayanda", "Nkosi", Roles.Admin);
        await EnsureUserAsync(userManager, "guest@staybnb.co.za", password, "Sipho", "Dlamini", Roles.Guest);
        var (applicant, _) = await EnsureUserAsync(userManager, "applicant@staybnb.co.za", password, "Naledi", "Khumalo", Roles.Guest);

        var hosts = new Dictionary<string, ApplicationUser>();
        for (var i = 0; i < Hosts.Length; i++)
        {
            var (email, first, last) = Hosts[i];
            hosts[i == 0 ? "host" : $"host{i + 1}"] = (await EnsureUserAsync(userManager, email, password, first, last, Roles.Host)).User;
        }

        var reviewers = new List<ApplicationUser>();
        foreach (var (first, last) in Reviewers)
        {
            var email = $"{first}.{last}".ToLowerInvariant().Replace(" ", "") + "@example.co.za";
            reviewers.Add((await EnsureUserAsync(userManager, email, password, first, last, Roles.Guest)).User);
        }

        var listings = JsonSerializer.Deserialize<List<SeedListing>>(await File.ReadAllTextAsync(listingsFile),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!;

        var amenities = await db.Amenities.ToDictionaryAsync(a => a.Name);
        var random = new Random(2026);
        var today = DateTime.Today;
        var approvedAt = DateTime.UtcNow.AddDays(-420);

        foreach (var l in listings)
        {
            var owner = l.Pending ? applicant : hosts[l.Host];
            var property = new HostProperty
            {
                HostId = owner.Id,
                Title = l.Title,
                Address = l.Address,
                City = l.City,
                Country = "South Africa",
                PropertyType = l.Type,
                PricePerNight = l.Price,
                CleaningFee = l.Cleaning,
                MaxGuests = l.Guests,
                Bedrooms = l.Bedrooms,
                Beds = l.Beds,
                Bathrooms = l.Baths,
                Description = l.Description,
                IsActive = !l.Pending,
                CreatedAt = l.Pending ? DateTime.UtcNow.AddDays(-2) : approvedAt,
                Amenities = l.Amenities.Where(amenities.ContainsKey).Select(a => amenities[a]).ToList(),
                Images = l.Photos.Select((n, i) => new PropertyImage { ImageUrl = $"/images/listings/{l.Key}-{n}.jpg", DisplayOrder = i }).ToList(),
                CheckInProcess = l.Pending ? null : CheckInSteps.CreateDefaultProcess()
            };

            db.HostApplications.Add(l.Pending
                ? new HostApplication { ApplicationUserId = owner.Id, Property = property, AppliedAt = DateTime.UtcNow.AddDays(-2) }
                : new HostApplication
                {
                    ApplicationUserId = owner.Id,
                    Property = property,
                    Status = ApplicationStatus.Approved,
                    AppliedAt = approvedAt.AddDays(-2),
                    ReviewedAt = approvedAt
                });

            if (!l.Pending)
                AddPastStays(db, property, owner, reviewers, random, today, serviceFeeRate);
        }

        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = admin.Id,
            Action = "DemoDataSeeded",
            ActivityType = ActivityType.System,
            Details = $"{listings.Count} demo listings with past stays and reviews created"
        });

        await db.SaveChangesAsync();
    }

    // Each review comes from a real completed, paid stay so ratings, earnings and revenue stay consistent.
    private static void AddPastStays(ApplicationDbContext db, HostProperty property, ApplicationUser host,
        List<ApplicationUser> reviewers, Random random, DateTime today, decimal serviceFeeRate)
    {
        var count = random.Next(2, 8);
        var guests = reviewers.OrderBy(_ => random.Next()).Take(count).ToList();

        // Walk backwards through the calendar so stays never overlap
        var checkOut = today.AddDays(-random.Next(5, 20));
        foreach (var guest in guests)
        {
            var nights = random.Next(2, 6);
            var checkIn = checkOut.AddDays(-nights);
            var subtotal = property.PricePerNight * nights;
            var serviceFee = Math.Round(subtotal * serviceFeeRate, 2);
            var total = subtotal + property.CleaningFee + serviceFee;
            var bookedAt = DateTime.SpecifyKind(checkIn.AddDays(-random.Next(7, 30)), DateTimeKind.Utc);

            db.Bookings.Add(new Booking
            {
                Property = property,
                GuestId = guest.Id,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                NumberOfGuests = Math.Min(property.MaxGuests, random.Next(1, 5)),
                PricePerNight = property.PricePerNight,
                CleaningFee = property.CleaningFee,
                ServiceFee = serviceFee,
                TotalPrice = total,
                Status = BookingStatus.Completed,
                CreatedAt = bookedAt,
                Payment = new Payment { Amount = total, Status = PaymentStatus.Paid, CreatedAt = bookedAt }
            });

            // Mostly 5 and 4 star reviews, with the occasional 3
            var roll = random.Next(100);
            var (rating, pool) = roll < 62 ? (5, GreatComments) : roll < 92 ? (4, GoodComments) : (3, OkComments);
            if (rating == 4 && random.Next(2) == 0) pool = GreatComments;

            db.Reviews.Add(new Review
            {
                Property = property,
                ReviewerId = guest.Id,
                Rating = rating,
                Comment = pool[random.Next(pool.Length)].Replace("{host}", host.FirstName).Replace("{city}", property.City),
                CreatedAt = DateTime.SpecifyKind(checkOut.AddDays(random.Next(1, 4)), DateTimeKind.Utc)
            });

            checkOut = checkIn.AddDays(-random.Next(10, 45));
        }
    }
}
