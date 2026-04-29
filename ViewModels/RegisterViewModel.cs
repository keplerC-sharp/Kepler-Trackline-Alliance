using System.ComponentModel.DataAnnotations;

namespace Kepler_Trackline_Alliance.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "El identificador es requerido")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Mínimo 3 caracteres")]
    public string Identifier { get; set; } = "";

    [Required(ErrorMessage = "El nombre completo es requerido")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "La contraseña es requerida")]
    [MinLength(4, ErrorMessage = "Mínimo 4 caracteres")]
    public string Password { get; set; } = "";
}
