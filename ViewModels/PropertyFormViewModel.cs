using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Staybnb.Models;

namespace Staybnb.ViewModels;

// Shared by the "Become a Host" application, "Add property" and "Edit property" forms
public class PropertyFormViewModel
{
    public int? Id { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Price per night (R)")]
    [Range(typeof(decimal), "1", "1000000")]
    public decimal PricePerNight { get; set; }

    [Display(Name = "Cleaning fee (R)")]
    [Range(typeof(decimal), "0", "100000")]
    public decimal CleaningFee { get; set; }

    [Required, MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Country { get; set; } = "South Africa";

    [Display(Name = "Property type")]
    public PropertyType PropertyType { get; set; }

    [Display(Name = "Max guests"), Range(1, 50)]
    public int MaxGuests { get; set; } = 2;

    [Range(0, 50)]
    public int Bedrooms { get; set; } = 1;

    [Range(1, 50)]
    public int Beds { get; set; } = 1;

    [Range(0, 50)]
    public int Bathrooms { get; set; } = 1;

    public List<int> AmenityIds { get; set; } = [];

    public List<IFormFile> Images { get; set; } = [];

    // Edit only
    public List<int> RemoveImageIds { get; set; } = [];

    [ValidateNever]
    public List<PropertyImage> ExistingImages { get; set; } = [];

    [ValidateNever]
    public List<Amenity> AvailableAmenities { get; set; } = [];

    public static PropertyFormViewModel FromProperty(HostProperty p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        Description = p.Description,
        PricePerNight = p.PricePerNight,
        CleaningFee = p.CleaningFee,
        Address = p.Address,
        City = p.City,
        Country = p.Country,
        PropertyType = p.PropertyType,
        MaxGuests = p.MaxGuests,
        Bedrooms = p.Bedrooms,
        Beds = p.Beds,
        Bathrooms = p.Bathrooms,
        AmenityIds = p.Amenities.Select(a => a.Id).ToList(),
        ExistingImages = p.Images.OrderBy(i => i.DisplayOrder).ToList()
    };

    public void ApplyTo(HostProperty p)
    {
        p.Title = Title.Trim();
        p.Description = Description.Trim();
        p.PricePerNight = PricePerNight;
        p.CleaningFee = CleaningFee;
        p.Address = Address.Trim();
        p.City = City.Trim();
        p.Country = Country.Trim();
        p.PropertyType = PropertyType;
        p.MaxGuests = MaxGuests;
        p.Bedrooms = Bedrooms;
        p.Beds = Beds;
        p.Bathrooms = Bathrooms;
    }
}
