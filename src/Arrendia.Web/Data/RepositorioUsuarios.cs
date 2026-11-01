using System.Data;
using System.Text.Json;
using Dapper;

namespace Arrendia.Web.Data;

public sealed class UsuarioFila
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string Rol { get; set; } = "";
    public bool Activo { get; set; }
    public bool TieneContrasena { get; set; }
    public bool InvitacionVigente { get; set; }
    public DateTime? UltimoAcceso { get; set; }
    public int Propiedades { get; set; }

    public string Acceso => TieneContrasena
        ? (Activo ? "Con acceso" : "Desactivado")
        : (InvitacionVigente ? "Invitación enviada" : "Invitación vencida");
}

public sealed class PerfilUsuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string Rol { get; set; } = "";
    public string TemaPreferido { get; set; } = "Sistema";
    public DateTime? UltimoAcceso { get; set; }
    public string? PasswordHash { get; set; }
    public string? CorreoPendiente { get; set; }
}

public sealed class PermisosSupervisor
{
    public bool PuedeCrearContratos { get; set; }
    public bool PuedeActivarContratos { get; set; }
    public bool PuedeCambiarCondiciones { get; set; }
    public bool PuedeRegistrarPagos { get; set; }
    public bool PuedeCargosManuales { get; set; }
    public bool PuedeCondonar { get; set; }
    public bool PuedeGestionarReportes { get; set; } = true;
}

public sealed class CambioCorreoFila
{
    public string EmailAnterior { get; set; } = "";
    public string EmailNuevo { get; set; } = "";
    public DateTime FechaCreacion { get; set; }
}

public interface IRepositorioUsuarios
{
    Task<int> InvitarAsync(string nombre, string email, string rol, int? inquilinoId, IReadOnlyCollection<int>? propiedades, byte[] tokenHash,
        bool enviarCorreo, int usuarioId, CancellationToken cancelacion);
    Task ReenviarAsync(int invitadoId, byte[] tokenHash, bool enviarCorreo, int usuarioId, CancellationToken cancelacion);
    Task RevocarAsync(int invitadoId, int usuarioId, CancellationToken cancelacion);
    Task<UsuarioFila?> ObtenerAsync(int id, CancellationToken cancelacion);
    Task<IReadOnlyList<UsuarioFila>> PersonalAsync(CancellationToken cancelacion);
    Task<int> AdministradoresActivosAsync(CancellationToken cancelacion);
    Task CambiarActivoAsync(int objetivoId, bool activo, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<int>> PropiedadesDeAsync(int supervisorId, CancellationToken cancelacion);
    Task<PermisosSupervisor> PermisosDeAsync(int supervisorId, CancellationToken cancelacion);
    Task GuardarSupervisorAsync(int supervisorId, IReadOnlyCollection<int> propiedades, PermisosSupervisor permisos, int usuarioId, CancellationToken cancelacion);
    Task<PerfilUsuario?> PerfilAsync(int id, CancellationToken cancelacion);
    Task<IReadOnlyList<CambioCorreoFila>> HistorialCorreoAsync(int id, CancellationToken cancelacion);
    Task ActualizarNombreAsync(int id, string nombre, CancellationToken cancelacion);
    Task GuardarTemaAsync(int id, string tema, CancellationToken cancelacion);
    Task<Guid> CambiarContrasenaAsync(int id, string passwordHash, CancellationToken cancelacion);
    Task<Guid> RenovarSelloAsync(int id, CancellationToken cancelacion);
    Task SolicitarCambioCorreoAsync(int objetivoId, string emailNuevo, byte[] tokenHash, int usuarioId, CancellationToken cancelacion);
    Task<(int UsuarioId, string Nombre, string EmailNuevo)?> CambioCorreoPendienteAsync(byte[] tokenHash, CancellationToken cancelacion);
    Task ConfirmarCambioCorreoAsync(byte[] tokenHash, CancellationToken cancelacion);
}

public sealed class RepositorioUsuarios(IBaseDatos baseDatos) : IRepositorioUsuarios
{
    private const string ConsultaFila = """
        SELECT u.ID, u.NOMBRE, u.EMAIL, u.ROL, u.ACTIVO, u.ULTIMO_ACCESO,
               CAST(CASE WHEN u.PASSWORD_HASH IS NULL THEN 0 ELSE 1 END AS BIT) AS TIENE_CONTRASENA,
               CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.TOKEN_USUARIO t WHERE t.USUARIO_ID = u.ID AND t.TIPO = 'Invitacion'
                                      AND t.USADO_EN IS NULL AND t.EXPIRA > SYSUTCDATETIME()) THEN 1 ELSE 0 END AS BIT) AS INVITACION_VIGENTE,
               (SELECT COUNT(*) FROM dbo.SUPERVISOR_PROPIEDAD s WHERE s.USUARIO_ID = u.ID AND s.ACTIVO = 1) AS PROPIEDADES
        FROM dbo.USUARIO u
        """;

    public async Task<int> InvitarAsync(string nombre, string email, string rol, int? inquilinoId, IReadOnlyCollection<int>? propiedades,
        byte[] tokenHash, bool enviarCorreo, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var p = new DynamicParameters(new
        {
            NOMBRE = nombre,
            EMAIL = email,
            ROL = rol,
            INQUILINO_ID = inquilinoId,
            PROPIEDADES = propiedades is null ? null : JsonSerializer.Serialize(propiedades),
            TOKEN_HASH = tokenHash,
            ENVIAR_CORREO = enviarCorreo,
            USUARIO_ID = usuarioId,
        });
        p.Add("USUARIO_NUEVO_ID", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CREAR_INVITACION", p, cancelacion)));
        return p.Get<int>("USUARIO_NUEVO_ID");
    }

    public async Task ReenviarAsync(int invitadoId, byte[] tokenHash, bool enviarCorreo, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_REENVIAR_INVITACION",
            new { USUARIO_INVITADO_ID = invitadoId, TOKEN_HASH = tokenHash, ENVIAR_CORREO = enviarCorreo, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task RevocarAsync(int invitadoId, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_REVOCAR_INVITACION",
            new { USUARIO_INVITADO_ID = invitadoId, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task<UsuarioFila?> ObtenerAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<UsuarioFila>(new CommandDefinition(
            ConsultaFila + " WHERE u.ID = @Id AND u.ROL <> 'Sistema'", new { Id = id }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<UsuarioFila>> PersonalAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<UsuarioFila>(new CommandDefinition(
            ConsultaFila + " WHERE u.ROL IN ('Administrador', 'Supervisor') ORDER BY u.ROL, u.NOMBRE", cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<int> AdministradoresActivosAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.USUARIO WHERE ROL = 'Administrador' AND ACTIVO = 1 AND PASSWORD_HASH IS NOT NULL", cancellationToken: cancelacion));
    }

    public async Task CambiarActivoAsync(int objetivoId, bool activo, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.USUARIO SET ACTIVO = @Activo, SELLO_SEGURIDAD = NEWID(), MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE ID = @Id AND PASSWORD_HASH IS NOT NULL AND ROL <> 'Sistema';
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID)
            VALUES (@UsuarioId, CASE WHEN @Activo = 1 THEN 'Activar' ELSE 'Desactivar' END, 'USUARIO', @Id);
            """,
            new { Id = objetivoId, Activo = activo, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<int>> PropiedadesDeAsync(int supervisorId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<int>(new CommandDefinition(
            "SELECT PROPIEDAD_ID FROM dbo.SUPERVISOR_PROPIEDAD WHERE USUARIO_ID = @Id AND ACTIVO = 1", new { Id = supervisorId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<PermisosSupervisor> PermisosDeAsync(int supervisorId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<PermisosSupervisor>(new CommandDefinition(
            """
            SELECT PUEDE_CREAR_CONTRATOS, PUEDE_ACTIVAR_CONTRATOS, PUEDE_CAMBIAR_CONDICIONES, PUEDE_REGISTRAR_PAGOS,
                   PUEDE_CARGOS_MANUALES, PUEDE_CONDONAR, PUEDE_GESTIONAR_REPORTES
            FROM dbo.PERMISO_SUPERVISOR WHERE USUARIO_ID = @Id
            """,
            new { Id = supervisorId }, cancellationToken: cancelacion)) ?? new PermisosSupervisor();
    }

    public async Task GuardarSupervisorAsync(int supervisorId, IReadOnlyCollection<int> propiedades, PermisosSupervisor permisos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await using var transaccion = await conexion.BeginTransactionAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.SUPERVISOR_PROPIEDAD
            SET ACTIVO = CASE WHEN PROPIEDAD_ID IN (SELECT TRY_CAST(value AS INT) FROM OPENJSON(@Propiedades)) THEN 1 ELSE 0 END,
                MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE USUARIO_ID = @Id;
            INSERT INTO dbo.SUPERVISOR_PROPIEDAD (USUARIO_ID, PROPIEDAD_ID, CREADO_POR)
            SELECT @Id, p.ID, @UsuarioId
            FROM dbo.PROPIEDAD p
            WHERE p.ID IN (SELECT TRY_CAST(value AS INT) FROM OPENJSON(@Propiedades))
              AND NOT EXISTS (SELECT 1 FROM dbo.SUPERVISOR_PROPIEDAD s WHERE s.USUARIO_ID = @Id AND s.PROPIEDAD_ID = p.ID);
            IF NOT EXISTS (SELECT 1 FROM dbo.PERMISO_SUPERVISOR WHERE USUARIO_ID = @Id)
                INSERT INTO dbo.PERMISO_SUPERVISOR (USUARIO_ID, CREADO_POR) VALUES (@Id, @UsuarioId);
            UPDATE dbo.PERMISO_SUPERVISOR
            SET PUEDE_CREAR_CONTRATOS = @PuedeCrearContratos, PUEDE_ACTIVAR_CONTRATOS = @PuedeActivarContratos,
                PUEDE_CAMBIAR_CONDICIONES = @PuedeCambiarCondiciones, PUEDE_REGISTRAR_PAGOS = @PuedeRegistrarPagos,
                PUEDE_CARGOS_MANUALES = @PuedeCargosManuales, PUEDE_CONDONAR = @PuedeCondonar, PUEDE_GESTIONAR_REPORTES = @PuedeGestionarReportes,
                MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE USUARIO_ID = @Id;
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID, DETALLE)
            VALUES (@UsuarioId, 'CambiarPermisos', 'USUARIO', @Id, (SELECT @Propiedades AS propiedades FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """,
            new
            {
                Id = supervisorId,
                UsuarioId = usuarioId,
                Propiedades = JsonSerializer.Serialize(propiedades),
                permisos.PuedeCrearContratos,
                permisos.PuedeActivarContratos,
                permisos.PuedeCambiarCondiciones,
                permisos.PuedeRegistrarPagos,
                permisos.PuedeCargosManuales,
                permisos.PuedeCondonar,
                permisos.PuedeGestionarReportes,
            },
            transaccion, cancellationToken: cancelacion));
        await transaccion.CommitAsync(cancelacion);
    }

    public async Task<PerfilUsuario?> PerfilAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<PerfilUsuario>(new CommandDefinition(
            """
            SELECT u.ID, u.NOMBRE, u.EMAIL, u.ROL, u.TEMA_PREFERIDO, u.ULTIMO_ACCESO, u.PASSWORD_HASH,
                   (SELECT TOP (1) t.EMAIL_NUEVO FROM dbo.TOKEN_USUARIO t WHERE t.USUARIO_ID = u.ID AND t.TIPO = 'CambioCorreo'
                      AND t.USADO_EN IS NULL AND t.EXPIRA > SYSUTCDATETIME() ORDER BY t.ID DESC) AS CORREO_PENDIENTE
            FROM dbo.USUARIO u WHERE u.ID = @Id
            """,
            new { Id = id }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<CambioCorreoFila>> HistorialCorreoAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<CambioCorreoFila>(new CommandDefinition(
            "SELECT EMAIL_ANTERIOR, EMAIL_NUEVO, FECHA_CREACION FROM dbo.USUARIO_CORREO_HISTORIAL WHERE USUARIO_ID = @Id ORDER BY ID DESC",
            new { Id = id }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task ActualizarNombreAsync(int id, string nombre, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.USUARIO SET NOMBRE = @Nombre, MODIFICADO_POR = @Id, FECHA_MODIFICACION = SYSUTCDATETIME() WHERE ID = @Id;
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID) VALUES (@Id, 'CambiarNombre', 'USUARIO', @Id);
            """,
            new { Id = id, Nombre = nombre }, cancellationToken: cancelacion));
    }

    public async Task GuardarTemaAsync(int id, string tema, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.USUARIO SET TEMA_PREFERIDO = @Tema WHERE ID = @Id", new { Id = id, Tema = tema }, cancellationToken: cancelacion));
    }

    public async Task<Guid> CambiarContrasenaAsync(int id, string passwordHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleAsync<Guid>(new CommandDefinition(
            """
            UPDATE dbo.USUARIO SET PASSWORD_HASH = @Hash, SELLO_SEGURIDAD = NEWID(), MODIFICADO_POR = @Id, FECHA_MODIFICACION = SYSUTCDATETIME()
            OUTPUT INSERTED.SELLO_SEGURIDAD
            WHERE ID = @Id;
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID) VALUES (@Id, 'CambiarContrasena', 'USUARIO', @Id);
            """,
            new { Id = id, Hash = passwordHash }, cancellationToken: cancelacion));
    }

    public async Task<Guid> RenovarSelloAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleAsync<Guid>(new CommandDefinition(
            """
            UPDATE dbo.USUARIO SET SELLO_SEGURIDAD = NEWID() OUTPUT INSERTED.SELLO_SEGURIDAD WHERE ID = @Id;
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID) VALUES (@Id, 'CerrarSesiones', 'USUARIO', @Id);
            """,
            new { Id = id }, cancellationToken: cancelacion));
    }

    public async Task SolicitarCambioCorreoAsync(int objetivoId, string emailNuevo, byte[] tokenHash, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_SOLICITAR_CAMBIO_CORREO",
            new { USUARIO_OBJETIVO_ID = objetivoId, EMAIL_NUEVO = emailNuevo, TOKEN_HASH = tokenHash, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task<(int UsuarioId, string Nombre, string EmailNuevo)?> CambioCorreoPendienteAsync(byte[] tokenHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var fila = await conexion.QuerySingleOrDefaultAsync<(int, string, string)?>(new CommandDefinition(
            """
            SELECT u.ID, u.NOMBRE, t.EMAIL_NUEVO
            FROM dbo.TOKEN_USUARIO t JOIN dbo.USUARIO u ON u.ID = t.USUARIO_ID
            WHERE t.TOKEN_HASH = @Hash AND t.TIPO = 'CambioCorreo' AND t.USADO_EN IS NULL AND t.EXPIRA > SYSUTCDATETIME()
            """,
            new { Hash = tokenHash }, cancellationToken: cancelacion));
        return fila;
    }

    public async Task ConfirmarCambioCorreoAsync(byte[] tokenHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CONFIRMAR_CAMBIO_CORREO",
            new { TOKEN_HASH = tokenHash }, cancelacion)));
    }
}
