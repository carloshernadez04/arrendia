using System.ComponentModel.DataAnnotations;
using Arrendia.Web.Data;

namespace Arrendia.Web.Models;

public sealed class InvitacionFormulario
{
    [Required(ErrorMessage = "Escribe el nombre.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
    public string? Nombre { get; set; }

    [Required(ErrorMessage = "Escribe el correo.")]
    [StringLength(254)]
    public string? Email { get; set; }

    public string Rol { get; set; } = "Supervisor";

    public List<int> Propiedades { get; set; } = [];

    public IReadOnlyList<PropiedadResumen> Opciones { get; set; } = [];
}

public sealed class UsuarioDetalleModelo
{
    public required UsuarioFila Usuario { get; init; }
    public required IReadOnlyList<PropiedadResumen> Opciones { get; init; }
    public required IReadOnlyList<int> Asignadas { get; init; }
    public required PermisosSupervisor Permisos { get; init; }
    public bool EsUnoMismo { get; init; }
}

public sealed class MiCuentaModelo
{
    public required PerfilUsuario Perfil { get; init; }
    public required IReadOnlyList<CambioCorreoFila> Historial { get; init; }
    public string? Seccion { get; init; }
}

public sealed class CambioContrasenaFormulario
{
    public string? Actual { get; set; }
    public string? Contrasena { get; set; }
    public string? Confirmacion { get; set; }
}

public sealed class ConfirmarCorreoModelo
{
    public required string Token { get; init; }
    public required string Nombre { get; init; }
    public required string EmailNuevo { get; init; }
}
