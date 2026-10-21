using System.ComponentModel.DataAnnotations;
using Arrendia.Web.Data;

namespace Arrendia.Web.Models;

public static class Catalogos
{
    public static readonly string[] TiposPropiedad = ["Comercial", "Residencial", "Mixta"];

    public static readonly string[] Departamentos =
    [
        "Ahuachapán", "Cabañas", "Chalatenango", "Cuscatlán", "La Libertad", "La Paz", "La Unión",
        "Morazán", "San Miguel", "San Salvador", "San Vicente", "Santa Ana", "Sonsonate", "Usulután",
    ];

    public static readonly string[] EstadosUnidad = ["Disponible", "Ocupada", "Mantenimiento", "Inactiva"];

    public static readonly string[] NivelesSugeridos = ["Sótano", "Planta baja", "Nivel 1", "Nivel 2", "Nivel 3", "Nivel 4", "Nivel 5"];

    public static string ClaseEstado(string estado) => estado switch
    {
        "Ocupada" => "info",
        "Disponible" => "ok",
        "Mantenimiento" => "warn",
        _ => "",
    };

    public static string TextoEstado(string estado) => estado == "Mantenimiento" ? "En mantenimiento" : estado;
}

public sealed class PropiedadFormulario
{
    public int? Id { get; set; }

    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "Escribe el nombre de la propiedad.")]
    [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
    public string? Nombre { get; set; }

    [Display(Name = "Tipo")]
    [Required(ErrorMessage = "Elige el tipo.")]
    public string? Tipo { get; set; } = "Comercial";

    [Display(Name = "Dirección")]
    [Required(ErrorMessage = "Escribe la dirección.")]
    [StringLength(250, ErrorMessage = "La dirección no puede pasar de 250 caracteres.")]
    public string? Direccion { get; set; }

    [Display(Name = "Municipio")]
    [Required(ErrorMessage = "Escribe el municipio.")]
    [StringLength(80, ErrorMessage = "El municipio no puede pasar de 80 caracteres.")]
    public string? Municipio { get; set; } = "Sonsonate";

    [Display(Name = "Departamento")]
    [Required(ErrorMessage = "Elige el departamento.")]
    public string? Departamento { get; set; } = "Sonsonate";

    [Display(Name = "Ubicación en el mapa")]
    [StringLength(2000)]
    public string? Ubicacion { get; set; }
}

public sealed class UnidadFormulario
{
    public int? Id { get; set; }

    [Display(Name = "Propiedad")]
    public int PropiedadId { get; set; }

    public string? PropiedadNombre { get; set; }

    public string? Volver { get; set; }

    public IReadOnlyList<PropiedadResumen> Propiedades { get; set; } = [];

    [Display(Name = "Código")]
    [Required(ErrorMessage = "Escribe el código de la unidad.")]
    [StringLength(20, ErrorMessage = "El código no puede pasar de 20 caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9ÁÉÍÓÚÑáéíóúñ .\-]+$", ErrorMessage = "Usa letras, números, espacios, puntos o guiones.")]
    public string? Codigo { get; set; }

    [Display(Name = "Tipo")]
    [Required(ErrorMessage = "Elige el tipo de unidad.")]
    public int? TipoUnidadId { get; set; }

    [Display(Name = "Nivel")]
    [StringLength(30, ErrorMessage = "El nivel no puede pasar de 30 caracteres.")]
    public string? Nivel { get; set; } = "Planta baja";

    [Display(Name = "Área (m²)")]
    [Range(typeof(decimal), "0.01", "99999", ErrorMessage = "El área debe ser mayor a 0.")]
    public decimal? AreaM2 { get; set; }

    [Display(Name = "Renta de referencia")]
    [Range(typeof(decimal), "0.01", "999999", ErrorMessage = "La renta debe ser mayor a $0.00.")]
    public decimal? RentaReferencia { get; set; }

    [Display(Name = "Aplica IVA (13 %)")]
    public bool AplicaIva { get; set; } = true;

    public IReadOnlyList<Opcion> Tipos { get; set; } = [];
}

public sealed class PropiedadDetalleModelo
{
    public required PropiedadResumen Propiedad { get; init; }
    public required IReadOnlyList<UnidadDetalle> Unidades { get; init; }
    public bool PuedeEditar { get; init; }
}

public sealed class UnidadesModelo
{
    public required IReadOnlyList<UnidadDetalle> Unidades { get; init; }
    public required IReadOnlyList<PropiedadResumen> Propiedades { get; init; }
    public required IReadOnlyList<Opcion> Tipos { get; init; }
    public FiltroUnidades Filtro { get; init; } = new();
    public bool PuedeEditar { get; init; }
}
