using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class Message
{
    public int Id { get; set; }

    [Required]
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;

    [Required]
    public string ReceiverId { get; set; } = string.Empty;
    public ApplicationUser Receiver { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    // Not in the ERD, but the rubric grades Read/Unread
    public bool IsRead { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
