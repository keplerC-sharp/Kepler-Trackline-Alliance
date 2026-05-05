using System.ComponentModel.DataAnnotations;

namespace Kepler_Trackline_Alliance.ViewModels;

/// <summary>
/// Data Transfer Object for authentication requests.
/// Encapsulates credentials and session persistence preferences.
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Marshal Identifier is required for authentication.")]
    public string Identifier { get; set; } = "";

    [Required(ErrorMessage = "Access Key is required for authentication.")]
    public string Password { get; set; } = "";

    public bool Remember { get; set; }
}
