using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<HostProperty> Properties => Set<HostProperty>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<HostApplication> HostApplications => Set<HostApplication>();
    public DbSet<CheckInProcess> CheckInProcesses => Set<CheckInProcess>();
    public DbSet<GuestCheckIn> GuestCheckIns => Set<GuestCheckIn>();
    public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Every decimal becomes decimal(18,2)
        b.Properties<decimal>().HavePrecision(18, 2);

        // Store enums as readable strings instead of ints
        b.Properties<PropertyType>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<BookingStatus>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<ApplicationStatus>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<CheckInStatus>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<DocumentStatus>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<ActivityType>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Must run first: configures the Identity tables
        base.OnModelCreating(builder);

        // Rule used below: FKs to AspNetUsers are Restrict (users are never hard-deleted),
        // and a property's own children cascade. This avoids SQL Server's
        // "multiple cascade paths" error.

        builder.Entity<HostProperty>(e =>
        {
            e.HasOne(p => p.Host)
             .WithMany(u => u.Properties)
             .HasForeignKey(p => p.HostId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(p => p.Amenities)
             .WithMany(a => a.Properties)
             .UsingEntity(j => j.ToTable("PropertyAmenities"));

            e.HasIndex(p => p.City);
            e.HasIndex(p => new { p.IsActive, p.PropertyType });
        });

        builder.Entity<PropertyImage>()
            .HasOne(i => i.Property)
            .WithMany(p => p.Images)
            .HasForeignKey(i => i.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Amenity>()
            .HasIndex(a => a.Name)
            .IsUnique();

        builder.Entity<Booking>(e =>
        {
            // Restrict: keep booking history; hosts deactivate properties instead of deleting
            e.HasOne(b => b.Property)
             .WithMany(p => p.Bookings)
             .HasForeignKey(b => b.PropertyId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Guest)
             .WithMany(u => u.Bookings)
             .HasForeignKey(b => b.GuestId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Payment)
             .WithOne(p => p.Booking)
             .HasForeignKey<Payment>(p => p.BookingId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(b => b.GuestCheckIn)
             .WithOne(c => c.Booking)
             .HasForeignKey<GuestCheckIn>(c => c.BookingId)
             .OnDelete(DeleteBehavior.Cascade);

            // Supports the date-overlap check when booking
            e.HasIndex(b => new { b.PropertyId, b.CheckInDate, b.CheckOutDate });

            e.ToTable(t => t.HasCheckConstraint("CK_Booking_Dates", "[CheckOutDate] > [CheckInDate]"));
        });

        builder.Entity<Review>(e =>
        {
            e.HasOne(r => r.Property)
             .WithMany(p => p.Reviews)
             .HasForeignKey(r => r.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Reviewer)
             .WithMany(u => u.Reviews)
             .HasForeignKey(r => r.ReviewerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] BETWEEN 1 AND 5"));
        });

        builder.Entity<WishlistItem>(e =>
        {
            e.HasOne(w => w.User)
             .WithMany(u => u.WishlistItems)
             .HasForeignKey(w => w.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(w => w.Property)
             .WithMany(p => p.WishlistItems)
             .HasForeignKey(w => w.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(w => new { w.UserId, w.PropertyId }).IsUnique();
        });

        builder.Entity<Message>(e =>
        {
            e.HasOne(m => m.Sender)
             .WithMany(u => u.SentMessages)
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Receiver)
             .WithMany(u => u.ReceivedMessages)
             .HasForeignKey(m => m.ReceiverId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(m => new { m.ReceiverId, m.IsRead });
        });

        builder.Entity<Notification>(e =>
        {
            e.HasOne(n => n.User)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(n => new { n.UserId, n.IsRead });
        });

        builder.Entity<ActivityLog>(e =>
        {
            e.HasOne(a => a.User)
             .WithMany(u => u.ActivityLogs)
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(a => a.CreatedAt);
        });

        builder.Entity<HostApplication>(e =>
        {
            e.HasOne(h => h.ApplicationUser)
             .WithMany(u => u.HostApplications)
             .HasForeignKey(h => h.ApplicationUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(h => h.Property)
             .WithMany(p => p.HostApplications)
             .HasForeignKey(h => h.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(h => h.Status);
        });

        builder.Entity<CheckInProcess>()
            .HasOne(c => c.Property)
            .WithOne(p => p.CheckInProcess)
            .HasForeignKey<CheckInProcess>(c => c.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<GuestCheckIn>()
            .HasOne(g => g.CheckInProcess)
            .WithMany(c => c.GuestCheckIns)
            .HasForeignKey(g => g.CheckInProcessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<GuestDocument>()
            .HasOne(d => d.GuestCheckIn)
            .WithMany(g => g.Documents)
            .HasForeignKey(d => d.GuestCheckInId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
