using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Staybnb.Models;

namespace Staybnb.ViewModels;

public class CheckInStepViewModel
{
    public GuestCheckIn CheckIn { get; set; } = null!;
    public List<CheckInStep> Steps { get; set; } = [];
    public int StepIndex { get; set; }
    public CheckInStep Step => Steps[StepIndex];
    public bool IsLastStep => StepIndex == Steps.Count - 1;
    public GuestDocument? ExistingDocument { get; set; }
}

public class CheckInProcessFormViewModel
{
    public int PropertyId { get; set; }

    [ValidateNever]
    public string PropertyTitle { get; set; } = string.Empty;

    [Required, MaxLength(200), Display(Name = "Process title")]
    public string Title { get; set; } = string.Empty;

    public List<CheckInStep> Steps { get; set; } = [];
}
