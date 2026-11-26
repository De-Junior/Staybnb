using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

namespace Staybnb.ViewModels;

public class PropertyCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public PropertyType PropertyType { get; set; }
    public decimal PricePerNight { get; set; }
    public List<string> ImageUrls { get; set; } = [];
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsWishlisted { get; set; }
}

public class HomeIndexViewModel
{
    public List<PropertyCardViewModel> Properties { get; set; } = [];
    public List<string> Cities { get; set; } = [];
    public string? City { get; set; }
    public PropertyType? Type { get; set; }
    public int? Guests { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public string? DateMessage { get; set; }
    public bool CanWishlist { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; }
}

public class PropertyDetailsViewModel
{
    public HostProperty Property { get; set; } = null!;
    public double AverageRating { get; set; }
    public bool CanBook { get; set; }
    public bool IsWishlisted { get; set; }
    public bool IsOwner { get; set; }
    public decimal ServiceFeeRate { get; set; }
    public BookingRequestViewModel Booking { get; set; } = new();
    public List<(DateTime From, DateTime To)> BookedRanges { get; set; } = [];
}

public class BookingRequestViewModel
{
    public int PropertyId { get; set; }

    [Display(Name = "Check in"), DataType(DataType.Date)]
    public DateTime CheckInDate { get; set; } = DateTime.Today.AddDays(1);

    [Display(Name = "Checkout"), DataType(DataType.Date)]
    public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(2);

    [Display(Name = "Guests"), Range(1, 50)]
    public int NumberOfGuests { get; set; } = 1;
}

public class ReviewFormViewModel
{
    public int BookingId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
