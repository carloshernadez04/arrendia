using Arrendia.Web.Data;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arrendia.Web.Controllers;

[Route("cuenta")]
public sealed partial class CuentaController(
    ServicioAcceso acceso,
    IRepositorioCuentas repositorio,
    ICorreo correo,
    ILogger<CuentaController> registro) : Controller
{
    public const string PoliticaLimite = "acceso";
    private const int MinutosRecuperacion = 60;

    [AllowAnonymous]
    [HttpGet("entrar")]
    public IActionResult Entrar(string? volver)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(ServicioAcceso.RutaInicial(User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value));
        }

        return View(new EntrarModelo { Volver = volver });
    }

    [AllowAnonymous]
    [HttpPost("entrar")]
    [EnableRateLimiting(PoliticaLimite)]
    public async Task<IActionResult> Entrar(EntrarModelo modelo, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(modelo.Email) || string.IsNullOrEmpty(modelo.Contrasena))
        {
            ModelState.AddModelError("", "Escribe tu correo y tu contraseña.");
            return View(modelo);
        }

        var resultado = await acceso.VerificarAsync(modelo.Email, modelo.Contrasena, cancelacion);
        if (resultado.Resultado == ResultadoIngreso.Bloqueado)
        {
            ModelState.AddModelError("", "Tu cuenta se bloqueó por varios intentos fallidos. Espera 15 minutos o crea una contraseña nueva.");
            return View(modelo);
        }

        if (resultado.Resultado != ResultadoIngreso.Correcto || resultado.Usuario is null)
        {
            ModelState.AddModelError("", "El correo o la contraseña no son correctos.");
            return View(modelo);
        }

        await IniciarSesionAsync(resultado.Usuario);
        return LocalRedirect(Url.IsLocalUrl(modelo.Volver) ? modelo.Volver : ServicioAcceso.RutaInicial(resultado.Usuario.Rol));
    }

    [HttpPost("salir")]
    public async Task<IActionResult> Salir()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Entrar));
    }

    [AllowAnonymous]
    [HttpGet("recuperar")]
    public IActionResult Recuperar() => View(new RecuperarModelo());

    [AllowAnonymous]
    [HttpPost("recuperar")]
    [EnableRateLimiting(PoliticaLimite)]
    public async Task<IActionResult> Recuperar(RecuperarModelo modelo, CancellationToken cancelacion)
    {
        var email = ReglasContrasena.NormalizarEmail(modelo.Email);
        if (!email.Contains('@', StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(modelo.Email), "Escribe un correo válido.");
            return View(modelo);
        }

        var token = TokenSeguro.Generar();
        await repositorio.SolicitarRecuperacionAsync(email, TokenSeguro.Hash(token), cancelacion);
        var pendiente = await repositorio.CorreoRecuperacionPendienteAsync(email, cancelacion);
        if (pendiente is not null)
        {
            var enlace = Url.Action(nameof(Restablecer), "Cuenta", new { token }, Request.Scheme) ?? "";
            try
            {
                await correo.EnviarAsync(PlantillasCorreo.Recuperacion(pendiente.Destinatario, pendiente.Nombre, enlace, MinutosRecuperacion), cancelacion);
                await repositorio.MarcarCorreoAsync(pendiente.Id, true, null, cancelacion);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                CorreoFallido(registro, pendiente.Id, error);
                await repositorio.MarcarCorreoAsync(pendiente.Id, false, error.Message, cancelacion);
            }
        }

        return View("Mensaje", new MensajeCuentaModelo
        {
            Titulo = "Revisa tu correo",
            Texto = $"Si {email} tiene una cuenta en Arrendia, te enviamos un enlace para crear una contraseña nueva. Vence en {MinutosRecuperacion} minutos.",
            Icono = "mail",
        });
    }

    [AllowAnonymous]
    [HttpGet("restablecer/{token}")]
    public async Task<IActionResult> Restablecer(string token, CancellationToken cancelacion)
    {
        var nombre = await NombreSiTokenValidoAsync(token, TipoToken.Recuperacion, cancelacion);
        return nombre is null ? EnlaceInvalido() : View(new NuevaContrasenaModelo { Token = token, Nombre = nombre });
    }

    [AllowAnonymous]
    [HttpPost("restablecer/{token}")]
    [EnableRateLimiting(PoliticaLimite)]
    public async Task<IActionResult> Restablecer(string token, NuevaContrasenaModelo modelo, CancellationToken cancelacion)
    {
        modelo.Token = token;
        if (!TokenSeguro.TieneFormatoValido(token))
        {
            return EnlaceInvalido();
        }

        var error = ReglasContrasena.Validar(modelo.Contrasena, modelo.Confirmacion);
        if (error is not null)
        {
            ModelState.AddModelError(nameof(modelo.Contrasena), error);
            return View(modelo);
        }

        try
        {
            await repositorio.RestablecerContrasenaAsync(TokenSeguro.Hash(token), acceso.CifrarContrasena(modelo.Contrasena!), cancelacion);
        }
        catch (ReglaDeNegocioException)
        {
            return EnlaceInvalido();
        }

        return View("Mensaje", new MensajeCuentaModelo
        {
            Titulo = "Contraseña actualizada",
            Texto = "Ya puedes entrar con tu contraseña nueva. Por seguridad, se cerraron las sesiones abiertas en otros dispositivos.",
        });
    }

    [AllowAnonymous]
    [HttpGet("activar/{token}")]
    public async Task<IActionResult> Activar(string token, CancellationToken cancelacion)
    {
        var nombre = await NombreSiTokenValidoAsync(token, TipoToken.Invitacion, cancelacion);
        return nombre is null ? EnlaceInvalido() : View(new NuevaContrasenaModelo { Token = token, Nombre = nombre });
    }

    [AllowAnonymous]
    [HttpPost("activar/{token}")]
    [EnableRateLimiting(PoliticaLimite)]
    public async Task<IActionResult> Activar(string token, NuevaContrasenaModelo modelo, CancellationToken cancelacion)
    {
        modelo.Token = token;
        if (!TokenSeguro.TieneFormatoValido(token))
        {
            return EnlaceInvalido();
        }

        var error = ReglasContrasena.Validar(modelo.Contrasena, modelo.Confirmacion);
        if (error is not null)
        {
            ModelState.AddModelError(nameof(modelo.Contrasena), error);
            return View(modelo);
        }

        UsuarioAcceso usuario;
        try
        {
            usuario = await repositorio.ActivarCuentaAsync(TokenSeguro.Hash(token), acceso.CifrarContrasena(modelo.Contrasena!), cancelacion);
        }
        catch (ReglaDeNegocioException)
        {
            return EnlaceInvalido();
        }

        await IniciarSesionAsync(usuario);
        return LocalRedirect(ServicioAcceso.RutaInicial(usuario.Rol));
    }

    [AllowAnonymous]
    [HttpGet("acceso-denegado")]
    public IActionResult AccesoDenegado() => View("Mensaje", new MensajeCuentaModelo
    {
        Titulo = "No tienes acceso a esta página",
        Texto = "Tu usuario no tiene permiso para ver esta sección. Si crees que es un error, pídeselo a la administración.",
        Icono = "lock",
    });

    private ViewResult EnlaceInvalido() => View("Mensaje", new MensajeCuentaModelo
    {
        Titulo = "Este enlace ya no sirve",
        Texto = "El enlace venció, ya se usó o no es válido. Pide uno nuevo a la administración o solicita otro desde «¿Olvidaste tu contraseña?».",
        Icono = "alert",
    });

    private async Task<string?> NombreSiTokenValidoAsync(string token, TipoToken tipo, CancellationToken cancelacion) =>
        TokenSeguro.TieneFormatoValido(token)
            ? await repositorio.NombreDeTokenVigenteAsync(TokenSeguro.Hash(token), tipo, cancelacion)
            : null;

    private Task IniciarSesionAsync(UsuarioAcceso usuario) =>
        HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            ServicioAcceso.CrearIdentidad(usuario),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo enviar el correo de recuperación {CorreoId}")]
    private static partial void CorreoFallido(ILogger registro, long correoId, Exception error);
}
