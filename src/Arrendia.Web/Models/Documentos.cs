using Arrendia.Web.Data;

namespace Arrendia.Web.Models;

public sealed class DocumentosModelo
{
    public required FiltroDocumentos Filtro { get; init; }
    public required IReadOnlyList<DocumentoFila> Documentos { get; init; }
}

public sealed class VerificarModelo
{
    public string? Codigo { get; set; }
    public bool Buscado { get; set; }
    public DocumentoVerificado? Documento { get; set; }
    public string? Comparacion { get; set; }
    public bool? Coincide { get; set; }
}

public sealed class Paginacion
{
    public int Pagina { get; init; } = 1;
    public int Tamano { get; init; } = 50;
    public int Total { get; init; }

    public int Paginas => Math.Max(1, (int)Math.Ceiling(Total / (double)Tamano));
    public bool HayAnterior => Pagina > 1;
    public bool HaySiguiente => Pagina < Paginas;
}
