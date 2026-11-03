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
            new("Propiedades", "building", "/propiedades"),
            new("Unidades", "door", "/unidades"),
            new("Inquilinos", "users", "/inquilinos"),
            new("Contratos", "file", "/contratos"),
            new("Técnicos", "wrench", "/tecnicos"),
            new("Usuarios y permisos", "shield", "/usuarios", SoloAdministrador: true),
            new("Configuración", "sliders", "/configuracion", SoloAdministrador: true),
        ]),
        new("Cobros",
        [
            new("Cargos", "receipt", "/cargos"),
            new("Pagos", "coin", "/pagos"),
            new("Cobranza", "alert", "/cobranza"),
        ]),
        new("Mantenimiento", [new("Reportes", "wrench", "/reportes")]),
        new("Consultas",
        [
            new("Informes", "chart", "/informes"),
            new("Documentos y QR", "qr", "/documentos"),
            new("Correos enviados", "mail", "/correos", SoloAdministrador: true),
            new("Bitácora", "history", "/bitacora", SoloAdministrador: true),
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
