using Arrendia.Web.Data;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Identity;

namespace Arrendia.Tests;

public class ServicioAccesoTests
{
    private const string Contrasena = "clave-segura-2026";
    private static readonly DateTime Ahora = new(2026, 10, 13, 15, 0, 0, DateTimeKind.Utc);

    private static (ServicioAcceso Servicio, RepositorioFalso Repositorio) Crear(Action<UsuarioAcceso>? ajustar = null)
    {
        var hasher = new PasswordHasher<UsuarioAcceso>();
        var usuario = new UsuarioAcceso
        {
            Id = 7,
            Nombre = "Laura Quintanilla",
            Email = "laura@arrendia.site",
            Rol = Roles.Administrador,
            Activo = true,
            SelloSeguridad = Guid.NewGuid(),
        };
        usuario.PasswordHash = hasher.HashPassword(usuario, Contrasena);
        ajustar?.Invoke(usuario);
        var repositorio = new RepositorioFalso(usuario);
        return (new ServicioAcceso(repositorio, hasher, new RelojFijo(Ahora)), repositorio);
    }

    [Fact]
    public async Task ConLaContrasenaCorrectaEntraYReiniciaLosIntentos()
    {
        var (servicio, repositorio) = Crear(u => u.IntentosFallidos = 3);

        var resultado = await servicio.VerificarAsync("  LAURA@arrendia.site ", Contrasena, CancellationToken.None);

        Assert.Equal(ResultadoIngreso.Correcto, resultado.Resultado);
        Assert.Equal(1, repositorio.Ingresos);
        Assert.Equal(0, repositorio.Usuario.IntentosFallidos);
    }

    [Fact]
    public async Task ConLaContrasenaIncorrectaSumaUnIntento()
    {
        var (servicio, repositorio) = Crear();

        var resultado = await servicio.VerificarAsync("laura@arrendia.site", "otra-clave-1", CancellationToken.None);

        Assert.Equal(ResultadoIngreso.Incorrecto, resultado.Resultado);
        Assert.Equal(1, repositorio.Usuario.IntentosFallidos);
        Assert.Null(repositorio.Usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task AlQuintoIntentoFallidoSeBloqueaQuinceMinutos()
    {
        var (servicio, repositorio) = Crear(u => u.IntentosFallidos = ServicioAcceso.IntentosMaximos - 1);

        var resultado = await servicio.VerificarAsync("laura@arrendia.site", "otra-clave-1", CancellationToken.None);

        Assert.Equal(ResultadoIngreso.Bloqueado, resultado.Resultado);
        Assert.Equal(Ahora.AddMinutes(15), repositorio.Usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task UnaCuentaBloqueadaNoEntraNiConLaContrasenaCorrecta()
    {
        var (servicio, repositorio) = Crear(u => u.BloqueadoHasta = Ahora.AddMinutes(5));

        var resultado = await servicio.VerificarAsync("laura@arrendia.site", Contrasena, CancellationToken.None);

        Assert.Equal(ResultadoIngreso.Bloqueado, resultado.Resultado);
        Assert.Equal(0, repositorio.Ingresos);
    }

    [Fact]
    public async Task CuandoTerminaElBloqueoPuedeEntrar()
    {
        var (servicio, _) = Crear(u => u.BloqueadoHasta = Ahora.AddMinutes(-1));

        var resultado = await servicio.VerificarAsync("laura@arrendia.site", Contrasena, CancellationToken.None);

        Assert.Equal(ResultadoIngreso.Correcto, resultado.Resultado);
    }

    [Theory]
    [InlineData("inactivo")]
    [InlineData("sin-contrasena")]
    [InlineData("sistema")]
    [InlineData("no-existe")]
    public async Task CuentasQueNoPuedenEntrarRecibenElMismoMensaje(string caso)
    {
        var (servicio, repositorio) = Crear(u =>
        {
            if (caso == "inactivo") { u.Activo = false; }
            if (caso == "sin-contrasena") { u.PasswordHash = null; }
            if (caso == "sistema") { u.Rol = "Sistema"; }
        });
        var email = caso == "no-existe" ? "nadie@correo.sv" : "laura@arrendia.site";

        var resultado = await servicio.VerificarAsync(email, Contrasena, CancellationToken.None);

        Assert.Equal(ResultadoIngreso.Incorrecto, resultado.Resultado);
        Assert.Equal(0, repositorio.Ingresos);
        Assert.Equal(0, repositorio.Usuario.IntentosFallidos);
    }

    [Fact]
    public void LaIdentidadLlevaRolSelloYPaginaInicial()
    {
        var (_, repositorio) = Crear(u => { u.Rol = Roles.Inquilino; u.InquilinoId = 12; });

        var identidad = ServicioAcceso.CrearIdentidad(repositorio.Usuario);

        Assert.True(identidad.IsInRole(Roles.Inquilino));
        Assert.Equal(repositorio.Usuario.SelloSeguridad.ToString(), identidad.FindFirst(Reclamos.Sello)?.Value);
        Assert.Equal("12", identidad.FindFirst(Reclamos.Inquilino)?.Value);
        Assert.Equal("/portal", ServicioAcceso.RutaInicial(Roles.Inquilino));
        Assert.Equal("/tablero", ServicioAcceso.RutaInicial(Roles.Supervisor));
    }

    private sealed class RelojFijo(DateTime ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(ahora);
    }

    private sealed class RepositorioFalso(UsuarioAcceso usuario) : IRepositorioCuentas
    {
        public UsuarioAcceso Usuario { get; } = usuario;
        public int Ingresos { get; private set; }

        public Task<UsuarioAcceso?> ObtenerPorEmailAsync(string email, CancellationToken cancelacion) =>
            Task.FromResult(email == Usuario.Email ? Usuario : null);

        public Task RegistrarFalloAsync(int id, DateTime? bloqueadoHasta, CancellationToken cancelacion)
        {
            Usuario.IntentosFallidos = bloqueadoHasta is null ? Usuario.IntentosFallidos + 1 : 0;
            Usuario.BloqueadoHasta = bloqueadoHasta;
            return Task.CompletedTask;
        }

        public Task RegistrarIngresoAsync(int id, string? nuevoHash, CancellationToken cancelacion)
        {
            Ingresos++;
            Usuario.IntentosFallidos = 0;
            Usuario.BloqueadoHasta = null;
            return Task.CompletedTask;
        }

        public Task<UsuarioAcceso?> ObtenerPorIdAsync(int id, CancellationToken cancelacion) => throw new NotSupportedException();
        public Task<EstadoSesion?> ObtenerEstadoSesionAsync(int id, CancellationToken cancelacion) => throw new NotSupportedException();
        public Task<string?> NombreDeTokenVigenteAsync(byte[] tokenHash, TipoToken tipo, CancellationToken cancelacion) => throw new NotSupportedException();
        public Task<UsuarioAcceso> ActivarCuentaAsync(byte[] tokenHash, string passwordHash, CancellationToken cancelacion) => throw new NotSupportedException();
        public Task SolicitarRecuperacionAsync(string email, byte[] tokenHash, CancellationToken cancelacion) => throw new NotSupportedException();
        public Task<CorreoPendiente?> CorreoRecuperacionPendienteAsync(string email, CancellationToken cancelacion) => throw new NotSupportedException();
        public Task MarcarCorreoAsync(long correoId, bool enviado, string? mensajeError, CancellationToken cancelacion) => throw new NotSupportedException();
        public Task RestablecerContrasenaAsync(byte[] tokenHash, string passwordHash, CancellationToken cancelacion) => throw new NotSupportedException();
        public Task<int?> AdministradorPendienteAsync(string? email, CancellationToken cancelacion) => throw new NotSupportedException();
        public Task GenerarInvitacionAsync(int usuarioInvitadoId, byte[] tokenHash, CancellationToken cancelacion) => throw new NotSupportedException();
    }
}
