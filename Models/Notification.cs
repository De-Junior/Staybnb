using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Body { get; set; }

    public NotificationType Type { get; set; }

    public bool IsRead { get; set; }

    // Target for the "Take Action" button
    [MaxLength(300)]
    public string? LinkUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
