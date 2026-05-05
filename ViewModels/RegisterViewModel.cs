using System.ComponentModel.DataAnnotations;

namespace Kepler_Trackline_Alliance.ViewModels;

/// <summary>
/// Data Transfer Object for operator registration.
/// Enforces business rules and validation constraints for security credentials.
/// </summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "Marshal Identifier is mandatory.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Identifier must be between 3 and 50 characters.")]
    public string Identifier { get; set; } = "";

    [Required(ErrorMessage = "Full Legal Name is mandatory.")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Security Access Key is mandatory.")]
    [MinLength(4, ErrorMessage = "Password must be at least 4 characters for basic entropy.")]
    public string Password { get; set; } = "";
}
