using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Staybnb.Models;

public class Booking
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;

    [Required]
    public string GuestId { get; set; } = string.Empty;
    public ApplicationUser Guest { get; set; } = null!;

    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }

    [Range(1, 50)]
    public int NumberOfGuests { get; set; } = 1;

    // Price snapshot at booking time, so later price edits don't change past bookings
    public decimal PricePerNight { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Payment? Payment { get; set; }
    public GuestCheckIn? GuestCheckIn { get; set; }

    [NotMapped]
    public int Nights => (CheckOutDate.Date - CheckInDate.Date).Days;
}
