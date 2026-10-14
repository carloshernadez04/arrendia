using System.Globalization;
using System.Security.Claims;
using Arrendia.Web.Data;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Arrendia.Web.Infraestructura;

public sealed class ValidacionSesion(IRepositorioCuentas repositorio) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sello = principal?.FindFirstValue(Reclamos.Sello);
        var rol = principal?.FindFirstValue(ClaimTypes.Role);

        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var usuarioId) || sello is null)
        {
            await Rechazar(context);
            return;
        }

        var estado = await repositorio.ObtenerEstadoSesionAsync(usuarioId, context.HttpContext.RequestAborted);
        if (estado is null || !estado.Activo || estado.SelloSeguridad.ToString() != sello || estado.Rol != rol)
        {
            await Rechazar(context);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (EsPeticionDeDatos(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return base.RedirectToLogin(context);
    }

    private static bool EsPeticionDeDatos(HttpRequest peticion) =>
        peticion.Headers.Accept.Any(valor => valor?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    private static async Task Rechazar(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
