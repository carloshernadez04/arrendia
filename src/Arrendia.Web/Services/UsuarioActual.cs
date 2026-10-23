using System.Globalization;
using System.Security.Claims;

namespace Arrendia.Web.Services;

public static class UsuarioActual
{
    public static int Id(this ClaimsPrincipal usuario) =>
        int.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("No hay sesión."), CultureInfo.InvariantCulture);

    public static bool EsAdministrador(this ClaimsPrincipal usuario) => usuario.IsInRole(Roles.Administrador);
}
