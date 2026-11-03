using System.ComponentModel.DataAnnotations;
using Arrendia.Web.Data;

namespace Arrendia.Web.Models;

public sealed class TecnicoFormulario
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Escribe el nombre.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
    public string? Nombre { get; set; }

    [StringLength(254)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Escribe el teléfono.")]
    public string? Telefono { get; set; }

    public bool Activo { get; set; } = true;

    public List<int> Especialidades { get; set; } = [];

    public IReadOnlyList<Opcion> Opciones { get; set; } = [];
}

public sealed class TecnicosModelo
{
    public required IReadOnlyList<TecnicoFila> Tecnicos { get; init; }
    public bool PuedeEditar { get; init; }
}

public sealed class ReporteFormulario
{
    [Required(ErrorMessage = "Elige dónde está el problema.")]
    public string? Lugar { get; set; }

    [StringLength(120, ErrorMessage = "Máximo 120 caracteres.")]
    public string? AreaComun { get; set; }

    [Required(ErrorMessage = "Elige la categoría.")]
    public int? CategoriaId { get; set; }

    public string Prioridad { get; set; } = "Media";

    [Required(ErrorMessage = "Escribe un título corto.")]
    [StringLength(120, ErrorMessage = "El título no puede pasar de 120 caracteres.")]
    public string? Titulo { get; set; }

    [Required(ErrorMessage = "Describe el problema.")]
    [StringLength(2000, ErrorMessage = "La descripción no puede pasar de 2000 caracteres.")]
    public string? Descripcion { get; set; }

    public List<IFormFile> Fotos { get; set; } = [];

    public IReadOnlyList<UnidadDetalle> Unidades { get; set; } = [];
    public IReadOnlyList<PropiedadResumen> Propiedades { get; set; } = [];
    public IReadOnlyList<Opcion> Categorias { get; set; } = [];
}

public sealed class ReportesModelo
{
    public required IReadOnlyList<ReporteFila> Reportes { get; init; }
    public required IReadOnlyList<PropiedadResumen> Propiedades { get; init; }
    public required FiltroReportes Filtro { get; init; }
}

public sealed class ReporteDetalleModelo
{
    public required ReporteDetalle Reporte { get; init; }
    public required IReadOnlyList<HistorialReporte> Historial { get; init; }
    public required IReadOnlyList<ArchivoInfo> Fotos { get; init; }
    public required IReadOnlyList<TecnicoFila> Tecnicos { get; init; }
    public required Permisos Permisos { get; init; }
    public string? Pin { get; init; }
}

public sealed class TecnicoPublicoModelo
{
    public required string Token { get; init; }
    public ReporteParaTecnico? Reporte { get; init; }
    public string? Error { get; init; }
    public string? Mensaje { get; init; }
}

public static class Prioridades
{
    public static readonly string[] Todas = ["Urgente", "Alta", "Media", "Baja"];
}
