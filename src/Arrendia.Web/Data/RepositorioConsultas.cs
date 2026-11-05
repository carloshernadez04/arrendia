using Dapper;

namespace Arrendia.Web.Data;

public sealed class Indicadores
{
    public decimal CobradoMes { get; set; }
    public decimal FacturadoMes { get; set; }
    public decimal PorCobrar { get; set; }
    public decimal Vencido { get; set; }
    public int UnidadesOcupadas { get; set; }
    public int UnidadesTotales { get; set; }
    public int ReportesAbiertos { get; set; }
    public int ContratosPorVencer { get; set; }

    public int Ocupacion => UnidadesTotales == 0 ? 0 : (int)Math.Round(100.0 * UnidadesOcupadas / UnidadesTotales);
    public int Cobranza => FacturadoMes == 0 ? 0 : (int)Math.Min(100, Math.Round(100 * CobradoMes / FacturadoMes));
}

public sealed class SerieMes
{
    public string Mes { get; set; } = "";
    public decimal Facturado { get; set; }
    public decimal Cobrado { get; set; }
}

public sealed class PorVencerFila
{
    public int ContratoId { get; set; }
    public string Numero { get; set; } = "";
    public string Inquilino { get; set; } = "";
    public string Unidad { get; set; } = "";
    public DateTime FechaFin { get; set; }
    public int DiasRestantes { get; set; }
}

public sealed class IngresoFila
{
    public string Mes { get; set; } = "";
    public string Propiedad { get; set; } = "";
    public string Metodo { get; set; } = "";
    public int Pagos { get; set; }
    public decimal Total { get; set; }
}

public sealed class ConceptoTotal
{
    public string Concepto { get; set; } = "";
    public decimal Total { get; set; }
}

public sealed class MorosidadFila
{
    public string Inquilino { get; set; } = "";
    public string Contrato { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Propiedad { get; set; } = "";
    public decimal De1A30 { get; set; }
    public decimal De31A60 { get; set; }
    public decimal De61A90 { get; set; }
    public decimal MasDe90 { get; set; }
    public decimal TotalVencido { get; set; }
    public int MaxDiasAtraso { get; set; }
}

public sealed class OcupacionFila
{
    public int PropiedadId { get; set; }
    public string Propiedad { get; set; } = "";
    public int Unidades { get; set; }
    public int Ocupadas { get; set; }
    public int Disponibles { get; set; }
    public int EnMantenimiento { get; set; }
    public decimal? PorcentajeOcupacion { get; set; }
}

public sealed class TrabajoTecnicoFila
{
    public string Tecnico { get; set; } = "";
    public int Reportes { get; set; }
    public int Terminados { get; set; }
    public decimal CostoTotal { get; set; }
    public int? HorasPromedio { get; set; }
}

public sealed class BitacoraFila
{
    public long Id { get; set; }
    public string? Usuario { get; set; }
    public string Accion { get; set; } = "";
    public string Tabla { get; set; } = "";
    public int? RegistroId { get; set; }
    public string? Detalle { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public sealed class EjecucionFila
{
    public string Proceso { get; set; } = "";
    public DateTime FechaProceso { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime? Fin { get; set; }
    public string Estado { get; set; } = "";
    public int Registros { get; set; }
    public string? Error { get; set; }
}

public sealed record FiltroBitacora(string? Tabla = null, string? Accion = null, int? UsuarioId = null, DateTime? Desde = null, DateTime? Hasta = null, int Pagina = 1);

public interface IRepositorioConsultas
{
    Task<(Indicadores Indicadores, IReadOnlyList<SerieMes> Serie)> TableroAsync(int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<PorVencerFila>> PorVencerAsync(int usuarioId, CancellationToken cancelacion);
    Task<(IReadOnlyList<IngresoFila> Filas, IReadOnlyList<ConceptoTotal> Conceptos)> IngresosAsync(DateTime desde, DateTime hasta, int? propiedadId, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<MorosidadFila>> MorosidadAsync(DateTime corte, int? propiedadId, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<OcupacionFila>> OcupacionAsync(int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<TrabajoTecnicoFila>> TrabajosAsync(DateTime desde, DateTime hasta, int usuarioId, CancellationToken cancelacion);
    Task<(IReadOnlyList<BitacoraFila> Filas, int Total)> BitacoraAsync(FiltroBitacora filtro, int tamano, CancellationToken cancelacion);
    Task<IReadOnlyList<string>> TablasBitacoraAsync(CancellationToken cancelacion);
    Task<IReadOnlyList<Opcion>> UsuariosBitacoraAsync(CancellationToken cancelacion);
    Task<IReadOnlyList<EjecucionFila>> EjecucionesAsync(CancellationToken cancelacion);
}

public sealed class RepositorioConsultas(IBaseDatos baseDatos) : IRepositorioConsultas
{
    public async Task<(Indicadores Indicadores, IReadOnlyList<SerieMes> Serie)> TableroAsync(int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        using var lector = await conexion.QueryMultipleAsync(EjecucionSegura.Procedimiento("dbo.SP_TABLERO", new { USUARIO_ID = usuarioId }, cancelacion));
        var indicadores = await lector.ReadSingleAsync<Indicadores>();
        var serie = (await lector.ReadAsync<SerieMes>()).AsList();
        return (indicadores, serie);
    }

    public async Task<IReadOnlyList<PorVencerFila>> PorVencerAsync(int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<PorVencerFila>(new CommandDefinition(
            """
            SELECT TOP (8) CONTRATO_ID, NUMERO, INQUILINO, UNIDAD, FECHA_FIN, DIAS_RESTANTES FROM dbo.VW_CONTRATOS_POR_VENCER
            WHERE dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, PROPIEDAD_ID) = 1 ORDER BY FECHA_FIN
            """,
            new { UsuarioId = usuarioId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<(IReadOnlyList<IngresoFila> Filas, IReadOnlyList<ConceptoTotal> Conceptos)> IngresosAsync(DateTime desde, DateTime hasta, int? propiedadId, int usuarioId,
        CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        using var lector = await conexion.QueryMultipleAsync(EjecucionSegura.Procedimiento("dbo.SP_INFORME_INGRESOS",
            new { DESDE = desde.Date, HASTA = hasta.Date, PROPIEDAD_ID = propiedadId, USUARIO_ID = usuarioId }, cancelacion));
        var filas = (await lector.ReadAsync<IngresoFila>()).AsList();
        var conceptos = (await lector.ReadAsync<ConceptoTotal>()).AsList();
        return (filas, conceptos);
    }

    public async Task<IReadOnlyList<MorosidadFila>> MorosidadAsync(DateTime corte, int? propiedadId, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<MorosidadFila>(EjecucionSegura.Procedimiento("dbo.SP_INFORME_MOROSIDAD",
            new { FECHA_CORTE = corte.Date, PROPIEDAD_ID = propiedadId, USUARIO_ID = usuarioId }, cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<OcupacionFila>> OcupacionAsync(int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<OcupacionFila>(new CommandDefinition(
            "SELECT * FROM dbo.VW_OCUPACION WHERE dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, PROPIEDAD_ID) = 1 ORDER BY PROPIEDAD",
            new { UsuarioId = usuarioId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<TrabajoTecnicoFila>> TrabajosAsync(DateTime desde, DateTime hasta, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<TrabajoTecnicoFila>(new CommandDefinition(
            """
            SELECT t.NOMBRE AS TECNICO, COUNT(r.ID) AS REPORTES,
                   SUM(CASE WHEN r.ESTADO IN ('Terminado', 'Cerrado') THEN 1 ELSE 0 END) AS TERMINADOS,
                   ISNULL(SUM(r.COSTO), 0) AS COSTO_TOTAL,
                   AVG(CASE WHEN r.FECHA_TERMINADO IS NOT NULL THEN DATEDIFF(HOUR, r.FECHA_ASIGNACION, r.FECHA_TERMINADO) END) AS HORAS_PROMEDIO
            FROM dbo.TECNICO t
            JOIN dbo.REPORTE_MANTENIMIENTO r ON r.TECNICO_ID = t.ID
            WHERE CAST(dbo.FN_A_HORA_SV(r.FECHA_ASIGNACION) AS DATE) BETWEEN @Desde AND @Hasta
              AND dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, r.PROPIEDAD_ID) = 1
            GROUP BY t.ID, t.NOMBRE
            ORDER BY REPORTES DESC
            """,
            new { Desde = desde.Date, Hasta = hasta.Date, UsuarioId = usuarioId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<(IReadOnlyList<BitacoraFila> Filas, int Total)> BitacoraAsync(FiltroBitacora filtro, int tamano, CancellationToken cancelacion)
    {
        const string Condicion = """
            WHERE (@Tabla IS NULL OR b.TABLA = @Tabla) AND (@Accion IS NULL OR b.ACCION LIKE @Accion)
              AND (@UsuarioId IS NULL OR b.USUARIO_ID = @UsuarioId)
              AND (@Desde IS NULL OR b.FECHA_CREACION >= @Desde) AND (@Hasta IS NULL OR b.FECHA_CREACION < DATEADD(DAY, 1, @Hasta))
            """;
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        using var lector = await conexion.QueryMultipleAsync(new CommandDefinition(
            $"""
            SELECT COUNT(*) FROM dbo.BITACORA b {Condicion};
            SELECT b.ID, u.NOMBRE AS USUARIO, b.ACCION, b.TABLA, b.REGISTRO_ID, b.DETALLE, b.FECHA_CREACION
            FROM dbo.BITACORA b LEFT JOIN dbo.USUARIO u ON u.ID = b.USUARIO_ID
            {Condicion}
            ORDER BY b.ID DESC OFFSET @Saltar ROWS FETCH NEXT @Tamano ROWS ONLY;
            """,
            new
            {
                filtro.Tabla,
                Accion = Texto.ComoPatron(filtro.Accion),
                filtro.UsuarioId,
                Desde = filtro.Desde?.Date.AddHours(6),
                Hasta = filtro.Hasta?.Date.AddHours(6),
                Saltar = (Math.Max(filtro.Pagina, 1) - 1) * tamano,
                Tamano = tamano,
            },
            cancellationToken: cancelacion));
        var total = await lector.ReadSingleAsync<int>();
        return ((await lector.ReadAsync<BitacoraFila>()).AsList(), total);
    }

    public async Task<IReadOnlyList<string>> TablasBitacoraAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<string>(new CommandDefinition("SELECT DISTINCT TABLA FROM dbo.BITACORA ORDER BY TABLA", cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<Opcion>> UsuariosBitacoraAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Opcion>(new CommandDefinition(
            "SELECT ID, NOMBRE + ' · ' + ROL AS NOMBRE FROM dbo.USUARIO ORDER BY NOMBRE", cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<EjecucionFila>> EjecucionesAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<EjecucionFila>(new CommandDefinition(
            "SELECT TOP (14) PROCESO, FECHA_PROCESO, INICIO, FIN, ESTADO, REGISTROS, ERROR FROM dbo.EJECUCION_PROCESO ORDER BY ID DESC",
            cancellationToken: cancelacion));
        return filas.AsList();
    }
}
