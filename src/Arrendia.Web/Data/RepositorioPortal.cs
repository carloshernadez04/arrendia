using System.Data;
using Arrendia.Web.Services;
using Dapper;

namespace Arrendia.Web.Data;

public sealed class IntencionPago
{
    public int Id { get; set; }
    public string Referencia { get; set; } = "";
    public int ContratoId { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "";
    public DateTime Expira { get; set; }
    public int? PagoId { get; set; }
    public string? NumeroPago { get; set; }
    public string? Error { get; set; }
}

public sealed class UnidadInquilino
{
    public int UnidadId { get; set; }
    public string Unidad { get; set; } = "";
    public int PropiedadId { get; set; }
    public string Propiedad { get; set; } = "";
}

public interface IRepositorioPortal
{
    Task<IReadOnlyList<ContratoResumen>> ContratosAsync(int inquilinoId, CancellationToken cancelacion);
    Task<IReadOnlyList<CargoFila>> PendientesAsync(int inquilinoId, CancellationToken cancelacion);
    Task<IReadOnlyList<PagoFila>> PagosAsync(int inquilinoId, CancellationToken cancelacion);
    Task<IReadOnlyList<UnidadInquilino>> UnidadesAsync(int inquilinoId, CancellationToken cancelacion);
    Task<(int Id, string Referencia)> CrearIntencionAsync(int contratoId, decimal monto, IReadOnlyCollection<int>? cargos, int usuarioId, CancellationToken cancelacion);
    Task<IntencionPago?> IntencionAsync(string referencia, int inquilinoId, CancellationToken cancelacion);
    Task<long> GuardarAvisoAsync(string? idTransaccion, string payload, bool firmaValida, CancellationToken cancelacion);
    Task MarcarAvisoAsync(long eventoId, string mensajeError, CancellationToken cancelacion);
    Task<string> ProcesarAvisoAsync(long eventoId, AvisoPasarela aviso, string respuesta, CancellationToken cancelacion);
}

public sealed class RepositorioPortal(IBaseDatos baseDatos) : IRepositorioPortal
{
    public async Task<IReadOnlyList<ContratoResumen>> ContratosAsync(int inquilinoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<ContratoResumen>(new CommandDefinition(
            """
            SELECT * FROM dbo.VW_RESUMEN_CONTRATO
            WHERE INQUILINO_ID = @Id AND (ESTADO IN ('Activo', 'PendienteFirma') OR DEUDA_TOTAL > 0)
            ORDER BY CASE ESTADO WHEN 'Activo' THEN 0 ELSE 1 END, FECHA_INICIO DESC
            """,
            new { Id = inquilinoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<CargoFila>> PendientesAsync(int inquilinoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<CargoFila>(new CommandDefinition(
            "SELECT * FROM dbo.VW_ESTADO_CUENTA WHERE INQUILINO_ID = @Id AND SALDO > 0 AND ESTADO IN ('Pendiente', 'Parcial') ORDER BY FECHA_VENCIMIENTO, CARGO_ID",
            new { Id = inquilinoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<PagoFila>> PagosAsync(int inquilinoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<PagoFila>(new CommandDefinition(
            "SELECT TOP (100) * FROM dbo.VW_HISTORIAL_PAGOS WHERE INQUILINO_ID = @Id ORDER BY FECHA_LOCAL DESC",
            new { Id = inquilinoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<UnidadInquilino>> UnidadesAsync(int inquilinoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<UnidadInquilino>(new CommandDefinition(
            """
            SELECT u.ID AS UNIDAD_ID, u.CODIGO AS UNIDAD, p.ID AS PROPIEDAD_ID, p.NOMBRE AS PROPIEDAD
            FROM dbo.CONTRATO c JOIN dbo.UNIDAD u ON u.ID = c.UNIDAD_ID JOIN dbo.PROPIEDAD p ON p.ID = u.PROPIEDAD_ID
            WHERE c.INQUILINO_ID = @Id AND c.ESTADO = 'Activo' ORDER BY p.NOMBRE, u.CODIGO
            """,
            new { Id = inquilinoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<(int Id, string Referencia)> CrearIntencionAsync(int contratoId, decimal monto, IReadOnlyCollection<int>? cargos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var p = new DynamicParameters(new
        {
            CONTRATO_ID = contratoId,
            MONTO = monto,
            CARGOS = cargos is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(cargos) : null,
            USUARIO_ID = usuarioId,
        });
        p.Add("INTENCION_ID", dbType: DbType.Int32, direction: ParameterDirection.Output);
        p.Add("REFERENCIA", dbType: DbType.AnsiString, size: 40, direction: ParameterDirection.Output);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CREAR_INTENCION_PAGO", p, cancelacion)));
        return (p.Get<int>("INTENCION_ID"), p.Get<string>("REFERENCIA"));
    }

    public async Task<IntencionPago?> IntencionAsync(string referencia, int inquilinoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<IntencionPago>(new CommandDefinition(
            """
            SELECT i.ID, i.REFERENCIA, i.CONTRATO_ID, i.MONTO, i.ESTADO, i.EXPIRA, i.PAGO_ID, p.NUMERO AS NUMERO_PAGO,
                   (SELECT TOP (1) w.ERROR FROM dbo.TRANSACCION_PASARELA t JOIN dbo.WEBHOOK_EVENTO w ON w.ID_TRANSACCION = t.ID_TRANSACCION
                    WHERE t.INTENCION_PAGO_ID = i.ID ORDER BY w.ID DESC) AS ERROR
            FROM dbo.INTENCION_PAGO i LEFT JOIN dbo.PAGO p ON p.ID = i.PAGO_ID
            WHERE i.REFERENCIA = @Referencia AND i.INQUILINO_ID = @Inquilino
            """,
            new { Referencia = referencia, Inquilino = inquilinoId }, cancellationToken: cancelacion));
    }

    public async Task<long> GuardarAvisoAsync(string? idTransaccion, string payload, bool firmaValida, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleAsync<long>(new CommandDefinition(
            """
            INSERT INTO dbo.WEBHOOK_EVENTO (EVENTO, ID_TRANSACCION, PAYLOAD, FIRMA_VALIDA)
            OUTPUT INSERTED.ID VALUES ('Wompi', LEFT(@Id, 64), @Payload, @Firma)
            """,
            new { Id = idTransaccion, Payload = payload, Firma = firmaValida }, cancellationToken: cancelacion));
    }

    public async Task MarcarAvisoAsync(long eventoId, string mensajeError, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.WEBHOOK_EVENTO SET PROCESADO = 1, FECHA_PROCESADO = SYSUTCDATETIME(), ERROR = @Error WHERE ID = @Id",
            new { Id = eventoId, Error = mensajeError }, cancellationToken: cancelacion));
    }

    public async Task<string> ProcesarAvisoAsync(long eventoId, AvisoPasarela aviso, string respuesta, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await EjecucionSegura.ConReglasAsync(() => ResultadoAsync(conexion.QueryMultipleAsync(EjecucionSegura.Procedimiento("dbo.SP_PROCESAR_WEBHOOK_PAGO", new
        {
            WEBHOOK_EVENTO_ID = eventoId,
            ID_TRANSACCION = aviso.IdTransaccion.Length > 64 ? aviso.IdTransaccion[..64] : aviso.IdTransaccion,
            REFERENCIA = aviso.Referencia,
            ESTADO_PASARELA = aviso.Aprobado ? "APPROVED" : "DECLINED",
            MONTO = aviso.Monto,
            MEDIO_PAGO = aviso.MedioPago is { Length: > 30 } m ? m[..30] : aviso.MedioPago,
            FECHA_TRANSACCION = aviso.Fecha,
            RESPUESTA = respuesta,
        }, cancelacion))));
    }

    private static async Task<string> ResultadoAsync(Task<SqlMapper.GridReader> consulta)
    {
        using var lector = await consulta;
        var resultado = "";
        while (!lector.IsConsumed)
        {
            if ((await lector.ReadAsync()).FirstOrDefault() is IDictionary<string, object> fila && fila.TryGetValue("RESULTADO", out var valor))
            {
                resultado = valor as string ?? "";
            }
        }

        return resultado;
    }
}
