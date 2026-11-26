using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class HostProperty
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "1000000")]
    public decimal PricePerNight { get; set; }

    // Not in the ERD, but required by the TotalPrice formula
    [Range(typeof(decimal), "0", "100000")]
    public decimal CleaningFee { get; set; }

    [Required, MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    // Not in the ERD, but required for filtering
    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Country { get; set; } = "South Africa";

    public PropertyType PropertyType { get; set; }

    [Range(1, 50)] public int MaxGuests { get; set; } = 1;
    [Range(0, 50)] public int Bedrooms { get; set; }
    [Range(1, 50)] public int Beds { get; set; } = 1;
    [Range(0, 50)] public int Bathrooms { get; set; }

    // False until the HostApplication is approved; host can toggle afterwards
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string HostId { get; set; } = string.Empty;
    public ApplicationUser Host { get; set; } = null!;

    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    public ICollection<HostApplication> HostApplications { get; set; } = new List<HostApplication>();
    public CheckInProcess? CheckInProcess { get; set; }
}
