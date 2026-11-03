using System.ComponentModel.DataAnnotations;
using Arrendia.Web.Data;
using Arrendia.Web.Services;

namespace Arrendia.Web.Models;

public sealed class CargosModelo
{
    public required IReadOnlyList<CargoFila> Cargos { get; init; }
    public required IReadOnlyList<PropiedadResumen> Propiedades { get; init; }
    public required FiltroCargos Filtro { get; init; }
    public required Permisos Permisos { get; init; }
}

public sealed class CargoDetalleModelo
{
    public required CargoFila Cargo { get; init; }
    public required IReadOnlyList<AjusteFila> Ajustes { get; init; }
    public required IReadOnlyList<AplicacionFila> Aplicaciones { get; init; }
    public int? ArchivoRespaldoId { get; init; }
    public required Permisos Permisos { get; init; }
}

public sealed class CargoFormulario
{
    [Required(ErrorMessage = "Elige el contrato.")]
    public int? ContratoId { get; set; }

    [Required(ErrorMessage = "Elige el concepto.")]
    public string? ConceptoCodigo { get; set; }

    [Required(ErrorMessage = "Escribe la descripción.")]
    [StringLength(200, ErrorMessage = "La descripción no puede pasar de 200 caracteres.")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "Escribe el monto.")]
    [Range(typeof(decimal), "0.01", "999999", ErrorMessage = "El monto debe ser mayor a $0.00.")]
    public decimal? MontoBase { get; set; }

    [Required(ErrorMessage = "Escribe el vencimiento.")]
    public DateTime? FechaVencimiento { get; set; }

    public string? GeneraMora { get; set; }

    public IFormFile? Respaldo { get; set; }

    public IReadOnlyList<Opcion> Contratos { get; set; } = [];
    public IReadOnlyList<Concepto> Conceptos { get; set; } = [];
}

public sealed class PagosModelo
{
    public required IReadOnlyList<PagoFila> Pagos { get; init; }
    public required IReadOnlyList<PropiedadResumen> Propiedades { get; init; }
    public required FiltroPagos Filtro { get; init; }
    public required Permisos Permisos { get; init; }

    public decimal Total => Pagos.Where(p => p.Estado == "Aplicado").Sum(p => p.Monto);
}

public sealed class PagoFormulario
{
    public int? ContratoId { get; set; }

    public decimal? Monto { get; set; }

    public string Metodo { get; set; } = "Efectivo";

    [StringLength(60, ErrorMessage = "La referencia no puede pasar de 60 caracteres.")]
    public string? Referencia { get; set; }

    public List<int> Cargos { get; set; } = [];

    public IFormFile? Comprobante { get; set; }

    public IReadOnlyList<Opcion> Contratos { get; set; } = [];
    public IReadOnlyList<CargoPendiente> Pendientes { get; set; } = [];
    public bool PermiteParciales { get; set; } = true;

    public decimal Deuda => ReglasPago.Deuda(Pendientes);
}

public sealed class PagoDetalleModelo
{
    public required PagoDetalle Pago { get; init; }
    public required IReadOnlyList<AplicacionFila> Aplicaciones { get; init; }
    public required Permisos Permisos { get; init; }
}

public sealed class CobranzaModelo
{
    public required IReadOnlyList<CobranzaFila> Filas { get; init; }
    public required IReadOnlyList<PropiedadResumen> Propiedades { get; init; }
    public int? PropiedadId { get; init; }
    public int MinimoDias { get; init; }
}
