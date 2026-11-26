using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class GuestDocument
{
    public int Id { get; set; }

    public int GuestCheckInId { get; set; }
    public GuestCheckIn GuestCheckIn { get; set; } = null!;

    [Required, MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    // Stored (GUID) file name, not the user's original name
    [Required, MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
