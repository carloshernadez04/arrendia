using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arrendia.Web.Controllers;

public sealed class MiCuentaController(
    IRepositorioUsuarios usuarios,
    IRepositorioCuentas cuentas,
    IPasswordHasher<UsuarioAcceso> hasher,
    ServicioAcceso acceso,
    EnvioInmediato envio,
    ServicioDocumentos documentos,
    IHostEnvironment ambiente) : Controller
{
    private static readonly Dictionary<string, string> Temas = new() { ["Sistema"] = "system", ["Claro"] = "light", ["Oscuro"] = "dark" };

    [HttpGet("mi-cuenta")]
    public async Task<IActionResult> Index(CancellationToken cancelacion)
    {
        var perfil = await usuarios.PerfilAsync(User.Id(), cancelacion);
        if (perfil is null)
        {
            return NotFound();
        }

        return View(new MiCuentaModelo { Perfil = perfil, Historial = await usuarios.HistorialCorreoAsync(perfil.Id, cancelacion) });
    }

    [HttpPost("mi-cuenta/nombre")]
    public async Task<IActionResult> Nombre(string? nombre, CancellationToken cancelacion)
    {
        nombre = nombre?.Trim();
        if (string.IsNullOrEmpty(nombre) || nombre.Length > 120)
        {
            this.Fallo("Escribe tu nombre (máximo 120 caracteres).");
            return RedirectToAction(nameof(Index));
        }

        await usuarios.ActualizarNombreAsync(User.Id(), nombre, cancelacion);
        await RenovarSesionAsync(cancelacion);
        this.Exito("Nombre actualizado.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("mi-cuenta/contrasena")]
    [EnableRateLimiting(CuentaController.PoliticaLimite)]
    public async Task<IActionResult> Contrasena(CambioContrasenaFormulario modelo, CancellationToken cancelacion)
    {
        var usuario = await cuentas.ObtenerPorIdAsync(User.Id(), cancelacion);
        if (usuario?.PasswordHash is null
            || hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, modelo.Actual ?? "") == PasswordVerificationResult.Failed)
        {
            this.Fallo("La contraseña actual no es correcta.");
            return RedirectToAction(nameof(Index), null, "contrasena");
        }

        var error = ReglasContrasena.Validar(modelo.Contrasena, modelo.Confirmacion);
        if (error is not null)
        {
            this.Fallo(error);
            return RedirectToAction(nameof(Index), null, "contrasena");
        }

        await usuarios.CambiarContrasenaAsync(usuario.Id, acceso.CifrarContrasena(modelo.Contrasena!), cancelacion);
        await RenovarSesionAsync(cancelacion);
        this.Exito("Contraseña cambiada. Se cerraron tus sesiones en otros dispositivos.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("mi-cuenta/sesiones")]
    public async Task<IActionResult> Sesiones(CancellationToken cancelacion)
    {
        await usuarios.RenovarSelloAsync(User.Id(), cancelacion);
        await RenovarSesionAsync(cancelacion);
        this.Exito("Listo: se cerraron tus sesiones en los demás dispositivos.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("mi-cuenta/tema")]
    public async Task<IActionResult> Tema(string? tema, CancellationToken cancelacion)
    {
        if (tema is null || !Temas.TryGetValue(tema, out var valor))
        {
            return BadRequest();
        }

        await usuarios.GuardarTemaAsync(User.Id(), tema, cancelacion);
        TempData["Tema"] = valor;
        this.Exito("Tema guardado.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("mi-cuenta/correo")]
    [EnableRateLimiting(CuentaController.PoliticaLimite)]
    public async Task<IActionResult> Correo(string? email, CancellationToken cancelacion)
    {
        email = email?.Trim().ToLowerInvariant();
        if (!ReglasInquilino.EmailValido(email))
        {
            this.Fallo("Escribe un correo válido.");
            return RedirectToAction(nameof(Index), null, "correo");
        }

        var token = TokenSeguro.Generar();
        try
        {
            await usuarios.SolicitarCambioCorreoAsync(User.Id(), email!, TokenSeguro.Hash(token), User.Id(), cancelacion);
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
            return RedirectToAction(nameof(Index), null, "correo");
        }

        var enlace = $"{documentos.UrlPublica()}/cuenta/confirmar-correo/{token}";
        var nombre = User.Identity?.Name ?? "";
        if (await envio.EnviarAsync("CambioCorreo", User.Id(), PlantillasCorreo.ConfirmarCorreo(email!, nombre, enlace), cancelacion))
        {
            this.Exito($"Te enviamos un enlace a {email}. Ábrelo para confirmar el cambio (vence en 24 horas).");
        }
        else if (ambiente.IsDevelopment())
        {
            this.EnlaceParaCopiar("Modo de desarrollo: abre este enlace para confirmar el correo nuevo.", enlace);
        }
        else
        {
            this.Fallo("No se pudo enviar el correo de confirmación. Inténtalo más tarde.");
        }

        return RedirectToAction(nameof(Index), null, "correo");
    }

    [AllowAnonymous]
    [HttpGet("cuenta/confirmar-correo/{token}")]
    public async Task<IActionResult> ConfirmarCorreo(string token, CancellationToken cancelacion)
    {
        var pendiente = TokenSeguro.TieneFormatoValido(token) ? await usuarios.CambioCorreoPendienteAsync(TokenSeguro.Hash(token), cancelacion) : null;
        return pendiente is { } p
            ? View(new ConfirmarCorreoModelo { Token = token, Nombre = p.Nombre, EmailNuevo = p.EmailNuevo })
            : View("~/Views/Cuenta/Mensaje.cshtml", new MensajeCuentaModelo
            {
                Titulo = "Este enlace ya no sirve",
                Texto = "El enlace venció, ya se usó o no es válido. Pide el cambio de correo otra vez desde Mi cuenta.",
                Icono = "alert",
            });
    }

    [AllowAnonymous]
    [HttpPost("cuenta/confirmar-correo/{token}")]
    [EnableRateLimiting(CuentaController.PoliticaLimite)]
    public async Task<IActionResult> ConfirmarCorreoPost(string token, CancellationToken cancelacion)
    {
        var pendiente = TokenSeguro.TieneFormatoValido(token) ? await usuarios.CambioCorreoPendienteAsync(TokenSeguro.Hash(token), cancelacion) : null;
        if (pendiente is not { } p)
        {
            return RedirectToAction(nameof(ConfirmarCorreo), new { token });
        }

        var anterior = (await usuarios.PerfilAsync(p.UsuarioId, cancelacion))?.Email;
        try
        {
            await usuarios.ConfirmarCambioCorreoAsync(TokenSeguro.Hash(token), cancelacion);
        }
        catch (ReglaDeNegocioException error)
        {
            return View("~/Views/Cuenta/Mensaje.cshtml", new MensajeCuentaModelo { Titulo = "No se pudo cambiar el correo", Texto = error.Message, Icono = "alert" });
        }

        if (anterior is not null)
        {
            await envio.EnviarAsync("CambioCorreo", p.UsuarioId, PlantillasCorreo.CorreoCambiado(anterior, p.Nombre, p.EmailNuevo), cancelacion);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return View("~/Views/Cuenta/Mensaje.cshtml", new MensajeCuentaModelo
        {
            Titulo = "Correo actualizado",
            Texto = $"Desde ahora entras con {p.EmailNuevo}. Por seguridad, se cerraron todas las sesiones abiertas.",
        });
    }

    private async Task RenovarSesionAsync(CancellationToken cancelacion)
    {
        var usuario = await cuentas.ObtenerPorIdAsync(User.Id(), cancelacion);
        if (usuario is not null)
        {
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ServicioAcceso.CrearIdentidad(usuario),
                new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
        }
    }
}
