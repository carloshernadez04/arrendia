using Arrendia.Web.Data;
using Arrendia.Web.Services;

namespace Arrendia.Web.Models;

public sealed class TableroModelo
{
    public required Indicadores Indicadores { get; init; }
    public required IReadOnlyList<SerieMes> Serie { get; init; }
    public required IReadOnlyList<PorVencerFila> PorVencer { get; init; }
    public required IReadOnlyList<CobranzaFila> Morosos { get; init; }
    public required IReadOnlyList<ReporteFila> Reportes { get; init; }
}

public sealed class InformesModelo
{
    public required string Tipo { get; init; }
    public required Informe Informe { get; init; }
    public required IReadOnlyList<ConceptoTotal> Conceptos { get; init; }
    public required DateTime Desde { get; init; }
    public required DateTime Hasta { get; init; }
    public int? PropiedadId { get; init; }
    public required IReadOnlyList<PropiedadResumen> Propiedades { get; init; }
}

public sealed class BitacoraModelo
{
    public required IReadOnlyList<BitacoraFila> Filas { get; init; }
    public required FiltroBitacora Filtro { get; init; }
    public required Paginacion Paginacion { get; init; }
    public required IReadOnlyList<string> Tablas { get; init; }
    public required IReadOnlyList<Opcion> Usuarios { get; init; }
    public required IReadOnlyList<EjecucionFila> Ejecuciones { get; init; }
}

public sealed class CorreosModelo
{
    public required IReadOnlyList<CorreoCola> Filas { get; init; }
    public required FiltroCorreos Filtro { get; init; }
    public required Paginacion Paginacion { get; init; }
    public bool ServidorListo { get; init; }
}
