using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models;

public class PropertyImage
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public HostProperty Property { get; set; } = null!;

    [Required, MaxLength(300)]
    public string ImageUrl { get; set; } = string.Empty;

    // 0 = cover image
    public int DisplayOrder { get; set; }
}
