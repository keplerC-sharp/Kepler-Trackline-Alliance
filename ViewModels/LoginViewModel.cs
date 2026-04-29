using System.ComponentModel.DataAnnotations;

namespace Kepler_Trackline_Alliance.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Ingresa tu Marshal ID")]
    public string Identifier { get; set; } = "";

    [Required(ErrorMessage = "Ingresa tu contraseña")]
    public string Password { get; set; } = "";

    public bool Remember { get; set; }
}
