using System.ComponentModel.DataAnnotations;
using Arrendia.Web.Data;

namespace Arrendia.Web.Models;

public sealed class InquilinoFormulario
{
    public int? Id { get; set; }

    [Display(Name = "Tipo de persona")]
    public string TipoPersona { get; set; } = "N";

    [Display(Name = "Nombre completo o razón social")]
    [Required(ErrorMessage = "Escribe el nombre.")]
    [StringLength(150, ErrorMessage = "El nombre no puede pasar de 150 caracteres.")]
    public string? Nombre { get; set; }

    [Display(Name = "DUI")]
    public string? Dui { get; set; }

    [Display(Name = "NIT")]
    public string? Nit { get; set; }

    [Display(Name = "NRC")]
    [StringLength(10, ErrorMessage = "El NRC no puede pasar de 10 caracteres.")]
    public string? Nrc { get; set; }

    [Display(Name = "Representante legal")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
    public string? RepresentanteLegal { get; set; }

    [Display(Name = "Teléfono")]
    public string? Telefono { get; set; }

    [Display(Name = "Dirección fiscal")]
    [StringLength(250, ErrorMessage = "La dirección no puede pasar de 250 caracteres.")]
    public string? DireccionFiscal { get; set; }

    [Display(Name = "Correo para avisos y comprobantes")]
    [StringLength(254)]
    public string? EmailContacto { get; set; }
}

public sealed class ContactoFormulario
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Escribe el nombre del contacto.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
    public string? Nombre { get; set; }

    [Required(ErrorMessage = "Escribe el correo.")]
    [StringLength(254)]
    public string? Email { get; set; }

    public string? Telefono { get; set; }

    public bool RecibeAvisos { get; set; } = true;
}

public sealed class InquilinosModelo
{
    public required IReadOnlyList<InquilinoFila> Inquilinos { get; init; }
    public required FiltroInquilinos Filtro { get; init; }
    public required Permisos Permisos { get; init; }
}

public sealed class InquilinoDetalleModelo
{
    public required InquilinoDetalle Inquilino { get; init; }
    public required IReadOnlyList<ContactoFila> Contactos { get; init; }
    public required IReadOnlyList<ContratoResumen> Contratos { get; init; }
    public required Permisos Permisos { get; init; }
    public ContactoFormulario NuevoContacto { get; init; } = new();
}
