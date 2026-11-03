using Arrendia.Web.Data;

namespace Arrendia.Web.Models;

public sealed class PortalModelo
{
    public required IReadOnlyList<ContratoResumen> Contratos { get; init; }
    public required IReadOnlyList<CargoFila> Pendientes { get; init; }
    public required IReadOnlyList<PagoFila> Pagos { get; init; }
    public required IReadOnlyList<ReporteFila> Reportes { get; init; }
    public bool PagoEnLinea { get; init; }

    public decimal Deuda => Pendientes.Sum(c => c.Saldo);
    public decimal Vencido => Pendientes.Where(c => c.Situacion == "Vencido").Sum(c => c.Saldo);
}

public sealed class PagarModelo
{
    public required IReadOnlyList<ContratoResumen> Contratos { get; init; }
    public required IReadOnlyList<CargoFila> Pendientes { get; init; }
    public int? ContratoId { get; init; }
    public decimal? Monto { get; init; }
    public IReadOnlyList<int> Cargos { get; init; } = [];
    public bool PermiteParciales { get; init; } = true;
    public string? Error { get; init; }
    public bool Disponible { get; init; }
}

public sealed class ResultadoPagoModelo
{
    public required IntencionPago Intencion { get; init; }
}

public sealed class SimuladorModelo
{
    public required IntencionPago Intencion { get; init; }
}

public sealed class DocumentosPortalModelo
{
    public required IReadOnlyList<DocumentoFila> Documentos { get; init; }
    public required IReadOnlyList<ContratoResumen> Contratos { get; init; }
}

public sealed class ReportesPortalModelo
{
    public required IReadOnlyList<ReporteFila> Reportes { get; init; }
}

public sealed class ReportePortalFormulario
{
    public int? UnidadId { get; set; }
    public int? CategoriaId { get; set; }
    public string Prioridad { get; set; } = "Media";
    public string? Titulo { get; set; }
    public string? Descripcion { get; set; }
    public List<IFormFile> Fotos { get; set; } = [];
    public IReadOnlyList<UnidadInquilino> Unidades { get; set; } = [];
    public IReadOnlyList<Opcion> Categorias { get; set; } = [];
    public string? Error { get; set; }
}
