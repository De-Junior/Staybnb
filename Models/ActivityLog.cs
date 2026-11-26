using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class ActivityLog
{
    public int Id { get; set; }

    // Nullable so system events and failed logins can still be logged
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    public ActivityType ActivityType { get; set; }

    [MaxLength(500)]
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
