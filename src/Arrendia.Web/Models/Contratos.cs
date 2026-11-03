using System.ComponentModel.DataAnnotations;
using Arrendia.Web.Data;
using Arrendia.Web.Services;

namespace Arrendia.Web.Models;

public sealed class ContratoFormulario
{
    [Display(Name = "Unidad")]
    [Required(ErrorMessage = "Elige la unidad.")]
    public int? UnidadId { get; set; }

    [Display(Name = "Inquilino")]
    [Required(ErrorMessage = "Elige el inquilino.")]
    public int? InquilinoId { get; set; }

    [Display(Name = "Inicio")]
    [Required(ErrorMessage = "Escribe la fecha de inicio.")]
    public DateTime? FechaInicio { get; set; }

    [Display(Name = "Fin")]
    [Required(ErrorMessage = "Escribe la fecha de fin.")]
    public DateTime? FechaFin { get; set; }

    [Display(Name = "Renta mensual (sin IVA)")]
    [Required(ErrorMessage = "Escribe la renta.")]
    [Range(typeof(decimal), "0.01", "999999", ErrorMessage = "La renta debe ser mayor a $0.00.")]
    public decimal? Renta { get; set; }

    [Display(Name = "Depósito")]
    [Range(typeof(decimal), "0", "999999", ErrorMessage = "El depósito no puede ser negativo.")]
    public decimal MontoDeposito { get; set; }

    [Display(Name = "Plantilla de condiciones")]
    public int? PlantillaId { get; set; }

    [Range(1, 31, ErrorMessage = "El día de pago va del 1 al 31.")]
    public int? DiaPago { get; set; }

    [Range(0, 60, ErrorMessage = "Los días de gracia van de 0 a 60.")]
    public int? DiasGracia { get; set; }

    [Range(typeof(decimal), "0", "5", ErrorMessage = "La mora diaria va de 0 % a 5 %.")]
    public decimal? PorcentajeMora { get; set; }

    public string? AplicaMora { get; set; }

    public string? PermiteParciales { get; set; }

    public IReadOnlyList<UnidadDetalle> Unidades { get; set; } = [];
    public IReadOnlyList<Opcion> Inquilinos { get; set; } = [];
    public IReadOnlyList<Plantilla> Plantillas { get; set; } = [];
}

public sealed class CondicionesFormulario
{
    [Required(ErrorMessage = "Escribe desde cuándo aplica.")]
    public DateTime? VigenteDesde { get; set; }

    [Range(typeof(decimal), "0.01", "999999", ErrorMessage = "La renta debe ser mayor a $0.00.")]
    public decimal? Renta { get; set; }

    [Range(1, 31, ErrorMessage = "El día de pago va del 1 al 31.")]
    public int? DiaPago { get; set; }

    [Range(0, 60, ErrorMessage = "Los días de gracia van de 0 a 60.")]
    public int? DiasGracia { get; set; }

    [Range(typeof(decimal), "0", "5", ErrorMessage = "La mora diaria va de 0 % a 5 %.")]
    public decimal? PorcentajeMora { get; set; }

    public string? AplicaMora { get; set; }

    public string? PermiteParciales { get; set; }

    [Required(ErrorMessage = "Escribe el motivo del cambio.")]
    [StringLength(500)]
    public string? Motivo { get; set; }
}

public sealed class ContratosModelo
{
    public required IReadOnlyList<ContratoResumen> Contratos { get; init; }
    public required IReadOnlyList<PropiedadResumen> Propiedades { get; init; }
    public required FiltroContratos Filtro { get; init; }
    public required Permisos Permisos { get; init; }
}

public sealed class ContratoDetalleModelo
{
    public required ContratoDetalle Contrato { get; init; }
    public required IReadOnlyList<Condicion> Condiciones { get; init; }
    public required IReadOnlyList<CargoFila> Cargos { get; init; }
    public required IReadOnlyList<PagoFila> Pagos { get; init; }
    public required IReadOnlyList<DocumentoFila> Documentos { get; init; }
    public required Permisos Permisos { get; init; }

    public Condicion? Vigente => Condiciones.FirstOrDefault(c => c.VigenteDesde <= Formato.HoySv()) ?? (Condiciones.Count > 0 ? Condiciones[^1] : null);
}

public sealed class EstadoCuentaModelo
{
    public required ContratoDetalle Contrato { get; init; }
    public required EstadoCuenta Estado { get; init; }
}

public static class Opciones
{
    public static bool? SiNo(string? valor) => valor switch
    {
        "si" => true,
        "no" => false,
        _ => null,
    };
}
