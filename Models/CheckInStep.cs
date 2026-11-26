using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

// One entry in CheckInProcess.StepsJson (not a table)
public class CheckInStep
{
    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Instructions { get; set; }

    public bool RequiresDocument { get; set; }

    [MaxLength(100)]
    public string? DocumentType { get; set; }
}
