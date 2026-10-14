using System.Globalization;
using System.Security.Claims;
using Arrendia.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Arrendia.Web.Services;

public enum ResultadoIngreso
{
    Correcto,
    Incorrecto,
    Bloqueado,
}

public sealed record ResultadoAcceso(ResultadoIngreso Resultado, UsuarioAcceso? Usuario = null, DateTime? BloqueadoHasta = null);

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Supervisor = "Supervisor";
    public const string Inquilino = "Inquilino";
    public const string Personal = Administrador + "," + Supervisor;
}

public static class Reclamos
{
    public const string Sello = "arrendia:sello";
    public const string Inquilino = "arrendia:inquilino";
}

public sealed class ServicioAcceso(IRepositorioCuentas repositorio, IPasswordHasher<UsuarioAcceso> hasher, TimeProvider reloj)
{
    public const int IntentosMaximos = 5;
    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    private static readonly UsuarioAcceso UsuarioFicticio = new();
    private static readonly string HashFicticio = new PasswordHasher<UsuarioAcceso>().HashPassword(UsuarioFicticio, "contrasena-ficticia-1");

    public async Task<ResultadoAcceso> VerificarAsync(string? email, string? contrasena, CancellationToken cancelacion)
    {
        var usuario = await repositorio.ObtenerPorEmailAsync(ReglasContrasena.NormalizarEmail(email), cancelacion);
        contrasena ??= "";

        if (usuario is null || usuario.PasswordHash is null || !usuario.Activo || usuario.Rol is not (Roles.Administrador or Roles.Supervisor or Roles.Inquilino))
        {
            hasher.VerifyHashedPassword(UsuarioFicticio, HashFicticio, contrasena);
            return new ResultadoAcceso(ResultadoIngreso.Incorrecto);
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        if (usuario.BloqueadoHasta > ahora)
        {
            return new ResultadoAcceso(ResultadoIngreso.Bloqueado, BloqueadoHasta: usuario.BloqueadoHasta);
        }

        var verificacion = hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, contrasena);
        if (verificacion == PasswordVerificationResult.Failed)
        {
            DateTime? bloqueo = usuario.IntentosFallidos + 1 >= IntentosMaximos ? ahora.Add(DuracionBloqueo) : null;
            await repositorio.RegistrarFalloAsync(usuario.Id, bloqueo, cancelacion);
            return bloqueo is null
                ? new ResultadoAcceso(ResultadoIngreso.Incorrecto)
                : new ResultadoAcceso(ResultadoIngreso.Bloqueado, BloqueadoHasta: bloqueo);
        }

        var nuevoHash = verificacion == PasswordVerificationResult.SuccessRehashNeeded ? hasher.HashPassword(usuario, contrasena) : null;
        await repositorio.RegistrarIngresoAsync(usuario.Id, nuevoHash, cancelacion);
        return new ResultadoAcceso(ResultadoIngreso.Correcto, usuario);
    }

    public string CifrarContrasena(string contrasena) => hasher.HashPassword(UsuarioFicticio, contrasena);

    public static ClaimsPrincipal CrearIdentidad(UsuarioAcceso usuario)
    {
        var reclamos = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Role, usuario.Rol),
            new(Reclamos.Sello, usuario.SelloSeguridad.ToString()),
        };
        if (usuario.InquilinoId is { } inquilino)
        {
            reclamos.Add(new Claim(Reclamos.Inquilino, inquilino.ToString(CultureInfo.InvariantCulture)));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(reclamos, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static string RutaInicial(string? rol) => rol == Roles.Inquilino ? "/portal" : "/tablero";
}
