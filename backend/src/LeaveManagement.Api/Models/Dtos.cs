using System.ComponentModel.DataAnnotations;

namespace LeaveManagement.Api.Models;

// Incoming payload for creating a leave request.
public class CreateLeaveRequestDto : IValidatableObject
{
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "Leave type is required.")]
    [EnumDataType(typeof(LeaveType), ErrorMessage = "Select a valid leave type.")]
    public LeaveType? Type { get; set; }

    [Required(ErrorMessage = "Start date is required.")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "End date is required.")]
    public DateTime? EndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
        {
            yield return new ValidationResult(
                "End date must be on or after start date.",
                [nameof(EndDate)]);
        }
    }
}
