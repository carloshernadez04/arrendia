using System.Data;
using Dapper;

namespace Arrendia.Web.Data;

public sealed class TecnicoFila
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Email { get; set; }
    public string Telefono { get; set; } = "";
    public bool Activo { get; set; }
    public string? Especialidades { get; set; }
    public int Reportes { get; set; }
    public int Terminados { get; set; }
    public int? HorasPromedio { get; set; }
}

public sealed class ReporteFila
{
    public int ReporteId { get; set; }
    public string Numero { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Prioridad { get; set; } = "";
    public string Estado { get; set; } = "";
    public int PropiedadId { get; set; }
    public string Propiedad { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public bool EsAreaComun { get; set; }
    public string Categoria { get; set; } = "";
    public string? Tecnico { get; set; }
    public decimal? Costo { get; set; }
    public DateTime FechaReporte { get; set; }
    public int DiasAbierto { get; set; }
}

public sealed class ReporteDetalle
{
    public int Id { get; set; }
    public string Numero { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Prioridad { get; set; } = "";
    public string Estado { get; set; } = "";
    public int PropiedadId { get; set; }
    public string Propiedad { get; set; } = "";
    public string Direccion { get; set; } = "";
    public int? UnidadId { get; set; }
    public string Ubicacion { get; set; } = "";
    public int? ContratoId { get; set; }
    public string? Inquilino { get; set; }
    public int CategoriaId { get; set; }
    public string Categoria { get; set; } = "";
    public int? EspecialidadId { get; set; }
    public string? Especialidad { get; set; }
    public int HorasAtencion { get; set; }
    public int? TecnicoId { get; set; }
    public string? Tecnico { get; set; }
    public string? TelefonoTecnico { get; set; }
    public decimal? Costo { get; set; }
    public bool Cobrado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaAsignacion { get; set; }
    public DateTime? FechaTerminado { get; set; }
    public DateTime? FechaCerrado { get; set; }
    public string Reporto { get; set; } = "";
    public bool EnlaceActivo { get; set; }
    public DateTime? EnlaceExpira { get; set; }
    public bool EnlaceBloqueado { get; set; }
}

public sealed class HistorialReporte
{
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = "";
    public string? Comentario { get; set; }
    public string? Quien { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public sealed class ReporteParaTecnico
{
    public string Numero { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Prioridad { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public string Propiedad { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string Municipio { get; set; } = "";
    public string Departamento { get; set; } = "";
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public bool PinVerificado { get; set; }
    public int IntentosRestantes { get; set; }
    public IReadOnlyList<FotoReporte> Fotos { get; set; } = [];
}

public sealed class FotoReporte
{
    public int Id { get; set; }
    public string PublicId { get; set; } = "";
    public string Categoria { get; set; } = "";
}

public sealed class ResultadoPin
{
    public bool Correcto { get; set; }
    public int? IntentosRestantes { get; set; }
    public bool Bloqueado { get; set; }
}

public sealed record DatosTecnico(string Nombre, string? Email, string Telefono, bool Activo, IReadOnlyCollection<int> Especialidades);

public sealed record NuevoReporte(int PropiedadId, int? UnidadId, string? AreaComun, int CategoriaId, string Prioridad, string Titulo, string Descripcion);

public sealed record FiltroReportes(string? Estado = null, string? Prioridad = null, int? PropiedadId = null, string? Buscar = null);

public interface IRepositorioMantenimiento
{
    Task<IReadOnlyList<TecnicoFila>> TecnicosAsync(bool soloActivos, CancellationToken cancelacion);
    Task<TecnicoFila?> TecnicoAsync(int id, CancellationToken cancelacion);
    Task<IReadOnlyList<int>> EspecialidadesDeAsync(int tecnicoId, CancellationToken cancelacion);
    Task<int> GuardarTecnicoAsync(int? id, DatosTecnico datos, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<TecnicoFila>> TecnicosParaAsync(int? especialidadId, CancellationToken cancelacion);
    Task<IReadOnlyList<Opcion>> CategoriasAsync(CancellationToken cancelacion);
    Task<IReadOnlyList<Opcion>> EspecialidadesAsync(CancellationToken cancelacion);
    Task<IReadOnlyList<ReporteFila>> ReportesAsync(int usuarioId, FiltroReportes filtro, CancellationToken cancelacion);
    Task<IReadOnlyList<ReporteFila>> ReportesDeInquilinoAsync(int inquilinoId, CancellationToken cancelacion);
    Task<ReporteDetalle?> ReporteAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<HistorialReporte>> HistorialAsync(int reporteId, CancellationToken cancelacion);
    Task<(int Id, string Numero, string Pin)> CrearReporteAsync(NuevoReporte datos, int usuarioId, CancellationToken cancelacion);
    Task AsignarAsync(int reporteId, int tecnicoId, byte[] tokenHash, bool enviarCorreo, int usuarioId, CancellationToken cancelacion);
    Task RevocarEnlaceAsync(int reporteId, int usuarioId, CancellationToken cancelacion);
    Task CambiarEstadoAsync(int reporteId, string estado, string? comentario, int usuarioId, CancellationToken cancelacion);
    Task RegistrarCostoAsync(int reporteId, decimal costo, bool cobrar, int usuarioId, CancellationToken cancelacion);
    Task<string?> PinAsync(int reporteId, int usuarioId, CancellationToken cancelacion);
    Task<ReporteParaTecnico> ParaTecnicoAsync(byte[] tokenHash, CancellationToken cancelacion);
    Task<ResultadoPin> ValidarPinAsync(byte[] tokenHash, string pin, CancellationToken cancelacion);
    Task CerrarPorTecnicoAsync(byte[] tokenHash, string? comentario, CancellationToken cancelacion);
    Task<(int EnlaceId, int ReporteId)?> EnlaceVigenteAsync(byte[] tokenHash, CancellationToken cancelacion);
}

public sealed class RepositorioMantenimiento(IBaseDatos baseDatos) : IRepositorioMantenimiento
{
    private const string ConsultaTecnicos = """
        SELECT t.ID, t.NOMBRE, t.EMAIL, t.TELEFONO, t.ACTIVO,
               (SELECT STRING_AGG(e.NOMBRE, ', ') WITHIN GROUP (ORDER BY e.NOMBRE)
                FROM dbo.TECNICO_ESPECIALIDAD x JOIN dbo.ESPECIALIDAD e ON e.ID = x.ESPECIALIDAD_ID WHERE x.TECNICO_ID = t.ID) AS ESPECIALIDADES,
               v.REPORTES, v.TERMINADOS, v.HORAS_PROMEDIO
        FROM dbo.TECNICO t JOIN dbo.VW_TRABAJOS_POR_TECNICO v ON v.TECNICO_ID = t.ID
        """;

    public async Task<IReadOnlyList<TecnicoFila>> TecnicosAsync(bool soloActivos, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<TecnicoFila>(new CommandDefinition(
            ConsultaTecnicos + " WHERE @Solo = 0 OR t.ACTIVO = 1 ORDER BY t.ACTIVO DESC, t.NOMBRE", new { Solo = soloActivos }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<TecnicoFila?> TecnicoAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<TecnicoFila>(new CommandDefinition(
            ConsultaTecnicos + " WHERE t.ID = @Id", new { Id = id }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<int>> EspecialidadesDeAsync(int tecnicoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<int>(new CommandDefinition(
            "SELECT ESPECIALIDAD_ID FROM dbo.TECNICO_ESPECIALIDAD WHERE TECNICO_ID = @Id", new { Id = tecnicoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<int> GuardarTecnicoAsync(int? id, DatosTecnico datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await using var transaccion = await conexion.BeginTransactionAsync(cancelacion);
        var tecnicoId = await conexion.QuerySingleAsync<int>(new CommandDefinition(
            """
            IF @Id IS NULL
                INSERT INTO dbo.TECNICO (NOMBRE, EMAIL, TELEFONO, ACTIVO, CREADO_POR) OUTPUT INSERTED.ID
                VALUES (@Nombre, @Email, @Telefono, @Activo, @UsuarioId);
            ELSE
                UPDATE dbo.TECNICO SET NOMBRE = @Nombre, EMAIL = @Email, TELEFONO = @Telefono, ACTIVO = @Activo,
                       MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
                OUTPUT INSERTED.ID WHERE ID = @Id;
            """,
            new { Id = id, datos.Nombre, datos.Email, datos.Telefono, datos.Activo, UsuarioId = usuarioId }, transaccion, cancellationToken: cancelacion));
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM dbo.TECNICO_ESPECIALIDAD WHERE TECNICO_ID = @Id AND ESPECIALIDAD_ID NOT IN (SELECT TRY_CAST(value AS INT) FROM OPENJSON(@Lista));
            INSERT INTO dbo.TECNICO_ESPECIALIDAD (TECNICO_ID, ESPECIALIDAD_ID)
            SELECT @Id, e.ID FROM dbo.ESPECIALIDAD e
            WHERE e.ID IN (SELECT TRY_CAST(value AS INT) FROM OPENJSON(@Lista))
              AND NOT EXISTS (SELECT 1 FROM dbo.TECNICO_ESPECIALIDAD x WHERE x.TECNICO_ID = @Id AND x.ESPECIALIDAD_ID = e.ID);
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID, DETALLE)
            VALUES (@UsuarioId, 'Guardar', 'TECNICO', @Id, (SELECT @Nombre AS nombre FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """,
            new { Id = tecnicoId, Lista = System.Text.Json.JsonSerializer.Serialize(datos.Especialidades), datos.Nombre, UsuarioId = usuarioId },
            transaccion, cancellationToken: cancelacion));
        await transaccion.CommitAsync(cancelacion);
        return tecnicoId;
    }

    public async Task<IReadOnlyList<TecnicoFila>> TecnicosParaAsync(int? especialidadId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<TecnicoFila>(new CommandDefinition(
            ConsultaTecnicos + """
                 WHERE t.ACTIVO = 1 AND (@Especialidad IS NULL OR EXISTS (SELECT 1 FROM dbo.TECNICO_ESPECIALIDAD x WHERE x.TECNICO_ID = t.ID AND x.ESPECIALIDAD_ID = @Especialidad))
                ORDER BY t.NOMBRE
                """,
            new { Especialidad = especialidadId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<Opcion>> CategoriasAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Opcion>(new CommandDefinition(
            "SELECT ID, NOMBRE FROM dbo.CATEGORIA_REPORTE WHERE ACTIVO = 1 ORDER BY NOMBRE", cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<Opcion>> EspecialidadesAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Opcion>(new CommandDefinition(
            "SELECT ID, NOMBRE FROM dbo.ESPECIALIDAD WHERE ACTIVO = 1 ORDER BY NOMBRE", cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<ReporteFila>> ReportesAsync(int usuarioId, FiltroReportes filtro, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<ReporteFila>(new CommandDefinition(
            """
            SELECT TOP (500) * FROM dbo.VW_REPORTES_MANTENIMIENTO
            WHERE dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, PROPIEDAD_ID) = 1
              AND (@Estado IS NULL OR ESTADO = @Estado OR (@Estado = 'Pendientes' AND ESTADO IN ('Abierto', 'Asignado', 'Terminado')))
              AND (@Prioridad IS NULL OR PRIORIDAD = @Prioridad)
              AND (@PropiedadId IS NULL OR PROPIEDAD_ID = @PropiedadId)
              AND (@Buscar IS NULL OR NUMERO LIKE @Buscar OR TITULO LIKE @Buscar OR UBICACION LIKE @Buscar OR TECNICO LIKE @Buscar)
            ORDER BY CASE ESTADO WHEN 'Abierto' THEN 0 WHEN 'Terminado' THEN 1 WHEN 'Asignado' THEN 2 ELSE 3 END,
                     CASE PRIORIDAD WHEN 'Urgente' THEN 0 WHEN 'Alta' THEN 1 WHEN 'Media' THEN 2 ELSE 3 END, FECHA_REPORTE DESC
            """,
            new { UsuarioId = usuarioId, filtro.Estado, filtro.Prioridad, filtro.PropiedadId, Buscar = Texto.ComoPatron(filtro.Buscar) },
            cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<ReporteFila>> ReportesDeInquilinoAsync(int inquilinoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<ReporteFila>(new CommandDefinition(
            """
            SELECT v.* FROM dbo.VW_REPORTES_MANTENIMIENTO v JOIN dbo.REPORTE_MANTENIMIENTO r ON r.ID = v.REPORTE_ID
            JOIN dbo.CONTRATO c ON c.ID = r.CONTRATO_ID
            WHERE c.INQUILINO_ID = @Id ORDER BY v.FECHA_REPORTE DESC
            """,
            new { Id = inquilinoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<ReporteDetalle?> ReporteAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<ReporteDetalle>(new CommandDefinition(
            """
            SELECT r.ID, r.NUMERO, r.TITULO, r.DESCRIPCION, r.PRIORIDAD, r.ESTADO, r.PROPIEDAD_ID, p.NOMBRE AS PROPIEDAD,
                   p.DIRECCION + ', ' + p.MUNICIPIO AS DIRECCION, r.UNIDAD_ID, COALESCE(u.CODIGO, r.UBICACION_AREA_COMUN) AS UBICACION,
                   r.CONTRATO_ID, i.NOMBRE AS INQUILINO, c.ID AS CATEGORIA_ID, c.NOMBRE AS CATEGORIA, c.ESPECIALIDAD_ID, e.NOMBRE AS ESPECIALIDAD,
                   c.HORAS_ATENCION, r.TECNICO_ID, t.NOMBRE AS TECNICO, t.TELEFONO AS TELEFONO_TECNICO, r.COSTO,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CARGO k WHERE k.REPORTE_MANTENIMIENTO_ID = r.ID AND k.ESTADO <> 'Anulado') THEN 1 ELSE 0 END AS BIT) AS COBRADO,
                   r.FECHA_CREACION, r.FECHA_ASIGNACION, r.FECHA_TERMINADO, r.FECHA_CERRADO, q.NOMBRE AS REPORTO,
                   CAST(CASE WHEN l.ID IS NULL THEN 0 ELSE 1 END AS BIT) AS ENLACE_ACTIVO, l.EXPIRA AS ENLACE_EXPIRA,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.ENLACE_TECNICO b WHERE b.REPORTE_ID = r.ID AND b.BLOQUEADO = 1 AND b.REVOCADO = 0) THEN 1 ELSE 0 END AS BIT) AS ENLACE_BLOQUEADO
            FROM dbo.REPORTE_MANTENIMIENTO r
            JOIN dbo.PROPIEDAD p ON p.ID = r.PROPIEDAD_ID
            JOIN dbo.CATEGORIA_REPORTE c ON c.ID = r.CATEGORIA_REPORTE_ID
            JOIN dbo.USUARIO q ON q.ID = r.CREADO_POR
            LEFT JOIN dbo.ESPECIALIDAD e ON e.ID = c.ESPECIALIDAD_ID
            LEFT JOIN dbo.UNIDAD u ON u.ID = r.UNIDAD_ID
            LEFT JOIN dbo.CONTRATO k ON k.ID = r.CONTRATO_ID
            LEFT JOIN dbo.INQUILINO i ON i.ID = k.INQUILINO_ID
            LEFT JOIN dbo.TECNICO t ON t.ID = r.TECNICO_ID
            OUTER APPLY (SELECT TOP (1) x.ID, x.EXPIRA FROM dbo.ENLACE_TECNICO x
                         WHERE x.REPORTE_ID = r.ID AND x.REVOCADO = 0 AND x.BLOQUEADO = 0 AND x.EXPIRA > SYSUTCDATETIME() ORDER BY x.ID DESC) l
            WHERE r.ID = @Id AND (dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, r.PROPIEDAD_ID) = 1
                                  OR (r.CONTRATO_ID IS NOT NULL AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, r.CONTRATO_ID) = 1))
            """,
            new { Id = id, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<HistorialReporte>> HistorialAsync(int reporteId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<HistorialReporte>(new CommandDefinition(
            """
            SELECT h.ESTADO_ANTERIOR, h.ESTADO_NUEVO, h.COMENTARIO, COALESCE(u.NOMBRE, t.NOMBRE + ' (técnico)') AS QUIEN, h.FECHA_CREACION
            FROM dbo.REPORTE_HISTORIAL h
            LEFT JOIN dbo.USUARIO u ON u.ID = h.USUARIO_ID
            LEFT JOIN dbo.ENLACE_TECNICO e ON e.ID = h.ENLACE_TECNICO_ID
            LEFT JOIN dbo.TECNICO t ON t.ID = e.TECNICO_ID
            WHERE h.REPORTE_ID = @Id ORDER BY h.ID
            """,
            new { Id = reporteId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<(int Id, string Numero, string Pin)> CrearReporteAsync(NuevoReporte datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var p = new DynamicParameters(new
        {
            PROPIEDAD_ID = datos.PropiedadId,
            UNIDAD_ID = datos.UnidadId,
            UBICACION_AREA_COMUN = datos.AreaComun,
            CATEGORIA_REPORTE_ID = datos.CategoriaId,
            PRIORIDAD = datos.Prioridad,
            TITULO = datos.Titulo,
            DESCRIPCION = datos.Descripcion,
            USUARIO_ID = usuarioId,
        });
        p.Add("REPORTE_ID", dbType: DbType.Int32, direction: ParameterDirection.Output);
        p.Add("NUMERO", dbType: DbType.AnsiString, size: 12, direction: ParameterDirection.Output);
        p.Add("PIN", dbType: DbType.AnsiStringFixedLength, size: 4, direction: ParameterDirection.Output);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CREAR_REPORTE", p, cancelacion)));
        return (p.Get<int>("REPORTE_ID"), p.Get<string>("NUMERO"), p.Get<string>("PIN"));
    }

    public async Task AsignarAsync(int reporteId, int tecnicoId, byte[] tokenHash, bool enviarCorreo, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_ASIGNAR_TECNICO",
            new { REPORTE_ID = reporteId, TECNICO_ID = tecnicoId, TOKEN_HASH = tokenHash, ENVIAR_CORREO = enviarCorreo, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task RevocarEnlaceAsync(int reporteId, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_REVOCAR_ENLACE_TECNICO",
            new { REPORTE_ID = reporteId, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task CambiarEstadoAsync(int reporteId, string estado, string? comentario, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CAMBIAR_ESTADO_REPORTE",
            new { REPORTE_ID = reporteId, ESTADO_NUEVO = estado, COMENTARIO = comentario, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task RegistrarCostoAsync(int reporteId, decimal costo, bool cobrar, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_REGISTRAR_COSTO_REPORTE",
            new { REPORTE_ID = reporteId, COSTO = costo, COBRAR_INQUILINO = cobrar, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task<string?> PinAsync(int reporteId, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        try
        {
            return await EjecucionSegura.ConReglasAsync(() => conexion.QuerySingleOrDefaultAsync<string?>(EjecucionSegura.Procedimiento("dbo.SP_OBTENER_PIN_REPORTE",
                new { REPORTE_ID = reporteId, USUARIO_ID = usuarioId }, cancelacion)));
        }
        catch (ReglaDeNegocioException)
        {
            return null;
        }
    }

    public async Task<ReporteParaTecnico> ParaTecnicoAsync(byte[] tokenHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        using var lector = await EjecucionSegura.ConReglasAsync(() => conexion.QueryMultipleAsync(EjecucionSegura.Procedimiento("dbo.SP_OBTENER_REPORTE_TECNICO",
            new { TOKEN_HASH = tokenHash }, cancelacion)));
        var reporte = await lector.ReadSingleAsync<ReporteParaTecnico>();
        reporte.Fotos = (await lector.ReadAsync<FotoReporte>()).AsList();
        return reporte;
    }

    public async Task<ResultadoPin> ValidarPinAsync(byte[] tokenHash, string pin, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await EjecucionSegura.ConReglasAsync(() => conexion.QuerySingleAsync<ResultadoPin>(EjecucionSegura.Procedimiento("dbo.SP_VALIDAR_PIN_TECNICO",
            new { TOKEN_HASH = tokenHash, PIN = pin }, cancelacion)));
    }

    public async Task CerrarPorTecnicoAsync(byte[] tokenHash, string? comentario, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CERRAR_REPORTE_TECNICO",
            new { TOKEN_HASH = tokenHash, COMENTARIO = comentario }, cancelacion)));
    }

    public async Task<(int EnlaceId, int ReporteId)?> EnlaceVigenteAsync(byte[] tokenHash, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var fila = await conexion.QuerySingleOrDefaultAsync<EnlaceFila>(new CommandDefinition(
            """
            SELECT ID, REPORTE_ID FROM dbo.ENLACE_TECNICO
            WHERE TOKEN_HASH = @Hash AND REVOCADO = 0 AND BLOQUEADO = 0 AND EXPIRA > SYSUTCDATETIME() AND PIN_VERIFICADO_EN IS NOT NULL
            """,
            new { Hash = tokenHash }, cancellationToken: cancelacion));
        return fila is null ? null : (fila.Id, fila.ReporteId);
    }

    private sealed class EnlaceFila
    {
        public int Id { get; set; }
        public int ReporteId { get; set; }
    }
}
