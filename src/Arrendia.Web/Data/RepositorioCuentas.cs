using Dapper;

namespace Arrendia.Web.Data;

public sealed class UsuarioAcceso
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string Rol { get; set; } = "";
    public int? InquilinoId { get; set; }
    public bool Activo { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public Guid SelloSeguridad { get; set; }
}

public sealed class EstadoSesion
{
    public bool Activo { get; set; }
    public Guid SelloSeguridad { get; set; }
    public string Rol { get; set; } = "";
}

public sealed class CorreoPendiente
{
    public long Id { get; set; }
    public string Destinatario { get; set; } = "";
    public string Nombre { get; set; } = "";
}

public enum TipoToken
{
    Invitacion,
    Recuperacion,
}

public interface IRepositorioCuentas
{
    Task<UsuarioAcceso?> ObtenerPorEmailAsync(string email, CancellationToken cancelacion);
    Task<UsuarioAcceso?> ObtenerPorIdAsync(int id, CancellationToken cancelacion);
    Task<EstadoSesion?> ObtenerEstadoSesionAsync(int id, CancellationToken cancelacion);
    Task RegistrarFalloAsync(int id, DateTime? bloqueadoHasta, CancellationToken cancelacion);
    Task RegistrarIngresoAsync(int id, string? nuevoHash, CancellationToken cancelacion);
    Task<string?> NombreDeTokenVigenteAsync(byte[] tokenHash, TipoToken tipo, CancellationToken cancelacion);
    Task<UsuarioAcceso> ActivarCuentaAsync(byte[] tokenHash, string passwordHash, CancellationToken cancelacion);
    Task SolicitarRecuperacionAsync(string email, byte[] tokenHash, CancellationToken cancelacion);
    Task<CorreoPendiente?> CorreoRecuperacionPendienteAsync(string email, CancellationToken cancelacion);
    Task MarcarCorreoAsync(long correoId, bool enviado, string? mensajeError, CancellationToken cancelacion);
    Task RestablecerContrasenaAsync(byte[] tokenHash, string passwordHash, CancellationToken cancelacion);
    Task<int?> AdministradorPendienteAsync(string? email, CancellationToken cancelacion);
    Task GenerarInvitacionAsync(int usuarioInvitadoId, byte[] tokenHash, CancellationToken cancelacion);
}

public sealed class RepositorioCuentas(IBaseDatos baseDatos) : IRepositorioCuentas
{
    private const string Columnas = """
        ID, NOMBRE, EMAIL, PASSWORD_HASH, ROL, INQUILINO_ID, ACTIVO, INTENTOS_FALLIDOS, BLOQUEADO_HASTA, SELLO_SEGURIDAD
        """;

    public async Task<UsuarioAcceso?> ObtenerPorEmailAsync(string email, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<UsuarioAcceso>(new CommandDefinition(
            $"SELECT {Columnas} FROM dbo.USUARIO WHERE EMAIL = @Email",
            new { Email = new DbString { Value = email, IsAnsi = false, Length = 254 } },
            cancellationToken: cancelacion));
    }

    public async Task<UsuarioAcceso?> ObtenerPorIdAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<UsuarioAcceso>(new CommandDefinition(
            $"SELECT {Columnas} FROM dbo.USUARIO WHERE ID = @Id", new { Id = id }, cancellationToken: cancelacion));
    }

    public async Task<EstadoSesion?> ObtenerEstadoSesionAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<EstadoSesion>(new CommandDefinition(
            "SELECT ACTIVO, SELLO_SEGURIDAD, ROL FROM dbo.USUARIO WHERE ID = @Id",
            new { Id = id }, cancellationToken: cancelacion));
    }

    public async Task RegistrarFalloAsync(int id, DateTime? bloqueadoHasta, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.USUARIO
            SET INTENTOS_FALLIDOS = CASE WHEN @Bloqueo IS NULL THEN INTENTOS_FALLIDOS + 1 ELSE 0 END,
                BLOQUEADO_HASTA = @Bloqueo
            WHERE ID = @Id
            """,
            new { Id = id, Bloqueo = bloqueadoHasta }, cancellationToken: cancelacion));
    }

    public async Task RegistrarIngresoAsync(int id, string? nuevoHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.USUARIO
            SET INTENTOS_FALLIDOS = 0, BLOQUEADO_HASTA = NULL, ULTIMO_ACCESO = SYSUTCDATETIME(),
                PASSWORD_HASH = COALESCE(@NuevoHash, PASSWORD_HASH)
            WHERE ID = @Id;
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID) VALUES (@Id, 'IniciarSesion', 'USUARIO', @Id);
            """,
            new { Id = id, NuevoHash = nuevoHash }, cancellationToken: cancelacion));
    }

    public async Task<string?> NombreDeTokenVigenteAsync(byte[] tokenHash, TipoToken tipo, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            SELECT u.NOMBRE
            FROM dbo.TOKEN_USUARIO t JOIN dbo.USUARIO u ON u.ID = t.USUARIO_ID
            WHERE t.TOKEN_HASH = @Hash AND t.TIPO = @Tipo AND t.USADO_EN IS NULL AND t.EXPIRA > SYSUTCDATETIME()
            """,
            new { Hash = tokenHash, Tipo = tipo.ToString() }, cancellationToken: cancelacion));
    }

    public async Task<UsuarioAcceso> ActivarCuentaAsync(byte[] tokenHash, string passwordHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var activado = await EjecucionSegura.ConReglasAsync(() => conexion.QuerySingleAsync<UsuarioAcceso>(
            EjecucionSegura.Procedimiento("dbo.SP_ACTIVAR_CUENTA", new { TOKEN_HASH = tokenHash, PASSWORD_HASH = passwordHash }, cancelacion)));
        return await ObtenerPorIdAsync(activado.Id, cancelacion) ?? activado;
    }

    public async Task SolicitarRecuperacionAsync(string email, byte[] tokenHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(
            EjecucionSegura.Procedimiento("dbo.SP_SOLICITAR_RECUPERACION", new { EMAIL = email, TOKEN_HASH = tokenHash }, cancelacion)));
    }

    public async Task<CorreoPendiente?> CorreoRecuperacionPendienteAsync(string email, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<CorreoPendiente>(new CommandDefinition(
            """
            SELECT TOP (1) c.ID, c.DESTINATARIO, u.NOMBRE
            FROM dbo.CORREO_ENVIADO c JOIN dbo.USUARIO u ON u.ID = c.USUARIO_ID
            WHERE c.TIPO = 'Recuperacion' AND c.ESTADO = 'Pendiente' AND u.EMAIL = @Email
            ORDER BY c.ID DESC
            """,
            new { Email = email }, cancellationToken: cancelacion));
    }

    public async Task MarcarCorreoAsync(long correoId, bool enviado, string? mensajeError, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.CORREO_ENVIADO
            SET ESTADO = CASE WHEN @Enviado = 1 THEN 'Enviado' ELSE 'Fallido' END, INTENTOS = INTENTOS + 1,
                FECHA_ENVIO = CASE WHEN @Enviado = 1 THEN SYSUTCDATETIME() ELSE NULL END, ERROR = LEFT(@Error, 1000)
            WHERE ID = @Id
            """,
            new { Id = correoId, Enviado = enviado, Error = mensajeError }, cancellationToken: cancelacion));
    }

    public async Task RestablecerContrasenaAsync(byte[] tokenHash, string passwordHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(
            EjecucionSegura.Procedimiento("dbo.SP_RESTABLECER_CONTRASENA", new { TOKEN_HASH = tokenHash, PASSWORD_HASH = passwordHash }, cancelacion)));
    }

    public async Task<int?> AdministradorPendienteAsync(string? email, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT TOP (1) ID FROM dbo.USUARIO
            WHERE ROL = 'Administrador' AND PASSWORD_HASH IS NULL AND (@Email IS NULL OR EMAIL = @Email)
            ORDER BY ID
            """,
            new { Email = email }, cancellationToken: cancelacion));
    }

    public async Task GenerarInvitacionAsync(int usuarioInvitadoId, byte[] tokenHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var sistema = await conexion.QuerySingleAsync<int>(new CommandDefinition("SELECT dbo.FN_ID_SISTEMA()", cancellationToken: cancelacion));
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento(
            "dbo.SP_REENVIAR_INVITACION",
            new { USUARIO_INVITADO_ID = usuarioInvitadoId, TOKEN_HASH = tokenHash, ENVIAR_CORREO = false, USUARIO_ID = sistema },
            cancelacion)));
    }
}
