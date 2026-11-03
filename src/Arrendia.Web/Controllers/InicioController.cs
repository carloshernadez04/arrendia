using System.Diagnostics;
using System.Security.Claims;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

public sealed class InicioController : Controller
{
    [AllowAnonymous]
    [HttpGet("/")]
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true
            ? LocalRedirect(ServicioAcceso.RutaInicial(User.FindFirstValue(ClaimTypes.Role)))
            : RedirectToAction("Entrar", "Cuenta");

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [Route("/error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(model: Activity.Current?.Id ?? HttpContext.TraceIdentifier);
}
