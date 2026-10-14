using System.ComponentModel.DataAnnotations;

namespace Arrendia.Web.Models;

public sealed class EntrarModelo
{
    [Display(Name = "Correo")]
    public string? Email { get; set; }

    [Display(Name = "Contraseña")]
    [DataType(DataType.Password)]
    public string? Contrasena { get; set; }

    public string? Volver { get; set; }
}

public sealed class RecuperarModelo
{
    [Display(Name = "Correo")]
    public string? Email { get; set; }
}

public sealed class NuevaContrasenaModelo
{
    public string Token { get; set; } = "";

    public string? Nombre { get; set; }

    [Display(Name = "Contraseña nueva")]
    [DataType(DataType.Password)]
    public string? Contrasena { get; set; }

    [Display(Name = "Repite la contraseña")]
    [DataType(DataType.Password)]
    public string? Confirmacion { get; set; }
}

public sealed class MensajeCuentaModelo
{
    public string Titulo { get; set; } = "";
    public string Texto { get; set; } = "";
    public string Icono { get; set; } = "check";
}
