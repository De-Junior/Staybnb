namespace Staybnb.Models;

public class GuestCheckIn
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public int CheckInProcessId { get; set; }
    public CheckInProcess CheckInProcess { get; set; } = null!;

    public CheckInStatus Status { get; set; } = CheckInStatus.NotStarted;

    // Index of the step the guest is on in the multi-step flow
    public int CurrentStep { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public ICollection<GuestDocument> Documents { get; set; } = new List<GuestDocument>();
}
