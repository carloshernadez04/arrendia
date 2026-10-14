using System.Security.Claims;
using Arrendia.Web.Services;

namespace Arrendia.Web.Models;

public sealed record ElementoMenu(string Texto, string Icono, string? Ruta, bool SoloAdministrador = false);

public sealed record GrupoMenu(string Titulo, IReadOnlyList<ElementoMenu> Elementos);

public static class Navegacion
{
    private static readonly IReadOnlyList<GrupoMenu> Personal =
    [
        new("Principal", [new("Tablero", "chart", "/tablero")]),
        new("Mantenimientos",
        [
            new("Propiedades", "building", null),
            new("Unidades", "door", null),
            new("Inquilinos", "users", null),
            new("Contratos", "file", null),
            new("Técnicos", "wrench", null),
            new("Usuarios y permisos", "shield", null, SoloAdministrador: true),
            new("Configuración", "sliders", null, SoloAdministrador: true),
        ]),
        new("Cobros",
        [
            new("Cargos", "receipt", null),
            new("Pagos", "coin", null),
            new("Cobranza", "alert", null),
        ]),
        new("Mantenimiento", [new("Reportes", "wrench", null)]),
        new("Consultas",
        [
            new("Informes", "chart", null),
            new("Documentos y QR", "qr", null),
            new("Correos enviados", "mail", null),
            new("Bitácora", "history", null),
        ]),
    ];

    public static IEnumerable<GrupoMenu> Para(ClaimsPrincipal usuario)
    {
        var esAdministrador = usuario.IsInRole(Roles.Administrador);
        foreach (var grupo in Personal)
        {
            var elementos = grupo.Elementos.Where(e => esAdministrador || !e.SoloAdministrador).ToList();
            if (elementos.Count > 0)
            {
                yield return grupo with { Elementos = elementos };
            }
        }
    }

    public static string Iniciales(string? nombre)
    {
        var palabras = (nombre ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(palabras.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }
}
