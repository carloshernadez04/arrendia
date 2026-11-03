using System.Data;
using Dapper;

namespace Arrendia.Web.Data;

public sealed class ContratoDetalle
{
    public int Id { get; set; }
    public string Numero { get; set; } = "";
    public string Estado { get; set; } = "";
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public decimal MontoDeposito { get; set; }
    public int? DocumentoContratoId { get; set; }
    public int? ArchivoFirmadoId { get; set; }
    public DateTime? FechaActivacion { get; set; }
    public DateTime? FechaTerminacion { get; set; }
    public string? MotivoTerminacion { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int UnidadId { get; set; }
    public string Unidad { get; set; } = "";
    public bool AplicaIva { get; set; }
    public int PropiedadId { get; set; }
    public string Propiedad { get; set; } = "";
    public int InquilinoId { get; set; }
    public string Inquilino { get; set; } = "";
    public decimal DeudaTotal { get; set; }
    public decimal DeudaVencida { get; set; }
    public DateTime? ProximoVencimiento { get; set; }
}

public sealed class Condicion
{
    public int Id { get; set; }
    public int NumeroVersion { get; set; }
    public DateTime VigenteDesde { get; set; }
    public DateTime? VigenteHasta { get; set; }
    public decimal Renta { get; set; }
    public int DiaPago { get; set; }
    public int DiasEmisionAviso { get; set; }
    public bool AplicaMora { get; set; }
    public int DiasGracia { get; set; }
    public decimal PorcentajeMoraDiario { get; set; }
    public string TipoTopeMora { get; set; } = "";
    public decimal? ValorTopeMora { get; set; }
    public bool PermitePagosParciales { get; set; }
    public bool CorrerVencimientoFeriado { get; set; }
    public string Motivo { get; set; } = "";
    public string? Plantilla { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public sealed class CargoFila
{
    public int CargoId { get; set; }
    public int ContratoId { get; set; }
    public string Contrato { get; set; } = "";
    public int InquilinoId { get; set; }
    public string Inquilino { get; set; } = "";
    public int UnidadId { get; set; }
    public string Unidad { get; set; } = "";
    public int PropiedadId { get; set; }
    public string Concepto { get; set; } = "";
    public string? Periodo { get; set; }
    public string Descripcion { get; set; } = "";
    public string Origen { get; set; } = "";
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal MontoBase { get; set; }
    public decimal MontoIva { get; set; }
    public decimal Monto { get; set; }
    public decimal Saldo { get; set; }
    public string Estado { get; set; } = "";
    public int DiasAtraso { get; set; }
    public string Situacion { get; set; } = "";
}

public sealed class PagoFila
{
    public int PagoId { get; set; }
    public string Numero { get; set; } = "";
    public DateTime FechaLocal { get; set; }
    public string Metodo { get; set; } = "";
    public string? Referencia { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "";
    public int ContratoId { get; set; }
    public string Contrato { get; set; } = "";
    public int InquilinoId { get; set; }
    public string Inquilino { get; set; } = "";
    public string Unidad { get; set; } = "";
    public int PropiedadId { get; set; }
    public int CargosAplicados { get; set; }
    public string? FolioComprobante { get; set; }
}

public sealed record NuevoContrato(int UnidadId, int InquilinoId, DateTime FechaInicio, DateTime FechaFin, decimal Renta, decimal MontoDeposito,
    int? PlantillaId, int? DiaPago, int? DiasGracia, bool? AplicaMora, decimal? PorcentajeMora, bool? PermiteParciales);

public sealed record CambioCondiciones(DateTime VigenteDesde, decimal? Renta, int? DiaPago, int? DiasGracia, bool? AplicaMora, decimal? PorcentajeMora,
    bool? PermiteParciales, string Motivo);

public sealed record FiltroContratos(string? Estado = null, int? PropiedadId = null, string? Buscar = null);

public interface IRepositorioContratos
{
    Task<IReadOnlyList<ContratoResumen>> ListarAsync(int usuarioId, FiltroContratos filtro, CancellationToken cancelacion);
    Task<IReadOnlyList<Opcion>> ActivosAsync(int usuarioId, CancellationToken cancelacion);
    Task<ContratoDetalle?> ObtenerAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<Condicion>> CondicionesAsync(int contratoId, CancellationToken cancelacion);
    Task<IReadOnlyList<CargoFila>> CargosAsync(int contratoId, CancellationToken cancelacion);
    Task<IReadOnlyList<PagoFila>> PagosAsync(int contratoId, CancellationToken cancelacion);
    Task<(int Id, string Numero)> CrearAsync(NuevoContrato datos, int usuarioId, CancellationToken cancelacion);
    Task ActivarAsync(int contratoId, int archivoFirmadoId, int usuarioId, CancellationToken cancelacion);
    Task CambiarCondicionesAsync(int contratoId, CambioCondiciones cambio, int usuarioId, CancellationToken cancelacion);
    Task FinalizarAsync(int contratoId, DateTime fecha, string motivo, int usuarioId, CancellationToken cancelacion);
    Task AnularAsync(int contratoId, string motivo, int usuarioId, CancellationToken cancelacion);
}

public sealed class RepositorioContratos(IBaseDatos baseDatos) : IRepositorioContratos
{
    public async Task<IReadOnlyList<ContratoResumen>> ListarAsync(int usuarioId, FiltroContratos filtro, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<ContratoResumen>(new CommandDefinition(
            """
            SELECT TOP (500) * FROM dbo.VW_RESUMEN_CONTRATO
            WHERE dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, PROPIEDAD_ID) = 1
              AND (@Estado IS NULL OR ESTADO = @Estado)
              AND (@PropiedadId IS NULL OR PROPIEDAD_ID = @PropiedadId)
              AND (@Buscar IS NULL OR NUMERO LIKE @Buscar OR INQUILINO LIKE @Buscar OR UNIDAD LIKE @Buscar OR PROPIEDAD LIKE @Buscar)
            ORDER BY CASE ESTADO WHEN 'PendienteFirma' THEN 0 WHEN 'Borrador' THEN 1 WHEN 'Activo' THEN 2 ELSE 3 END, FECHA_INICIO DESC
            """,
            new { UsuarioId = usuarioId, filtro.Estado, filtro.PropiedadId, Buscar = Texto.ComoPatron(filtro.Buscar) },
            cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<Opcion>> ActivosAsync(int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Opcion>(new CommandDefinition(
            """
            SELECT CONTRATO_ID AS ID, NUMERO + ' · ' + INQUILINO + ' · ' + UNIDAD + ' (' + PROPIEDAD + ')' AS NOMBRE
            FROM dbo.VW_RESUMEN_CONTRATO
            WHERE ESTADO = 'Activo' AND dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, PROPIEDAD_ID) = 1
            ORDER BY INQUILINO, NUMERO
            """,
            new { UsuarioId = usuarioId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<ContratoDetalle?> ObtenerAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<ContratoDetalle>(new CommandDefinition(
            """
            SELECT c.ID, c.NUMERO, c.ESTADO, c.FECHA_INICIO, c.FECHA_FIN, c.MONTO_DEPOSITO, c.DOCUMENTO_CONTRATO_ID, c.ARCHIVO_FIRMADO_ID,
                   c.FECHA_ACTIVACION, c.FECHA_TERMINACION, c.MOTIVO_TERMINACION, c.FECHA_CREACION,
                   u.ID AS UNIDAD_ID, u.CODIGO AS UNIDAD, u.APLICA_IVA, p.ID AS PROPIEDAD_ID, p.NOMBRE AS PROPIEDAD,
                   i.ID AS INQUILINO_ID, i.NOMBRE AS INQUILINO,
                   r.DEUDA_TOTAL, r.DEUDA_VENCIDA, r.PROXIMO_VENCIMIENTO
            FROM dbo.CONTRATO c
            JOIN dbo.UNIDAD u ON u.ID = c.UNIDAD_ID
            JOIN dbo.PROPIEDAD p ON p.ID = u.PROPIEDAD_ID
            JOIN dbo.INQUILINO i ON i.ID = c.INQUILINO_ID
            JOIN dbo.VW_RESUMEN_CONTRATO r ON r.CONTRATO_ID = c.ID
            WHERE c.ID = @Id AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, c.ID) = 1
            """,
            new { Id = id, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<Condicion>> CondicionesAsync(int contratoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Condicion>(new CommandDefinition(
            """
            SELECT k.ID, k.NUMERO_VERSION, k.VIGENTE_DESDE, k.VIGENTE_HASTA, k.RENTA, k.DIA_PAGO, k.DIAS_EMISION_AVISO, k.APLICA_MORA, k.DIAS_GRACIA,
                   k.PORCENTAJE_MORA_DIARIO, k.TIPO_TOPE_MORA, k.VALOR_TOPE_MORA, k.PERMITE_PAGOS_PARCIALES, k.CORRER_VENCIMIENTO_FERIADO, k.MOTIVO,
                   p.NOMBRE AS PLANTILLA, k.FECHA_CREACION
            FROM dbo.CONDICION_CONTRATO k LEFT JOIN dbo.PLANTILLA_CONDICIONES p ON p.ID = k.PLANTILLA_ORIGEN_ID
            WHERE k.CONTRATO_ID = @Id ORDER BY k.NUMERO_VERSION DESC
            """,
            new { Id = contratoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<CargoFila>> CargosAsync(int contratoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<CargoFila>(new CommandDefinition(
            "SELECT * FROM dbo.VW_ESTADO_CUENTA WHERE CONTRATO_ID = @Id ORDER BY FECHA_VENCIMIENTO DESC, CARGO_ID DESC",
            new { Id = contratoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<PagoFila>> PagosAsync(int contratoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<PagoFila>(new CommandDefinition(
            "SELECT * FROM dbo.VW_HISTORIAL_PAGOS WHERE CONTRATO_ID = @Id ORDER BY FECHA_LOCAL DESC",
            new { Id = contratoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<(int Id, string Numero)> CrearAsync(NuevoContrato datos, int usuarioId, CancellationToken cancelacion)
    {
        var d = datos;
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var p = new DynamicParameters(new
        {
            UNIDAD_ID = d.UnidadId,
            INQUILINO_ID = d.InquilinoId,
            FECHA_INICIO = d.FechaInicio.Date,
            FECHA_FIN = d.FechaFin.Date,
            RENTA = d.Renta,
            MONTO_DEPOSITO = d.MontoDeposito,
            PLANTILLA_ID = d.PlantillaId,
            DIA_PAGO = (byte?)d.DiaPago,
            DIAS_GRACIA = (byte?)d.DiasGracia,
            APLICA_MORA = d.AplicaMora,
            PORCENTAJE_MORA = d.PorcentajeMora,
            PERMITE_PARCIALES = d.PermiteParciales,
            USUARIO_ID = usuarioId,
        });
        p.Add("CONTRATO_ID", dbType: DbType.Int32, direction: ParameterDirection.Output);
        p.Add("NUMERO", dbType: DbType.AnsiString, size: 20, direction: ParameterDirection.Output);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CREAR_CONTRATO", p, cancelacion)));
        return (p.Get<int>("CONTRATO_ID"), p.Get<string>("NUMERO"));
    }

    public async Task ActivarAsync(int contratoId, int archivoFirmadoId, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_ACTIVAR_CONTRATO",
            new { CONTRATO_ID = contratoId, ARCHIVO_FIRMADO_ID = archivoFirmadoId, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task CambiarCondicionesAsync(int contratoId, CambioCondiciones cambio, int usuarioId, CancellationToken cancelacion)
    {
        var c = cambio;
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CAMBIAR_CONDICIONES",
            new
            {
                CONTRATO_ID = contratoId,
                VIGENTE_DESDE = c.VigenteDesde.Date,
                RENTA = c.Renta,
                DIA_PAGO = (byte?)c.DiaPago,
                DIAS_GRACIA = (byte?)c.DiasGracia,
                APLICA_MORA = c.AplicaMora,
                PORCENTAJE_MORA = c.PorcentajeMora,
                PERMITE_PARCIALES = c.PermiteParciales,
                MOTIVO = c.Motivo,
                USUARIO_ID = usuarioId,
            },
            cancelacion)));
    }

    public async Task FinalizarAsync(int contratoId, DateTime fecha, string motivo, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_FINALIZAR_CONTRATO",
            new { CONTRATO_ID = contratoId, FECHA_TERMINACION = fecha.Date, MOTIVO = motivo, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task AnularAsync(int contratoId, string motivo, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_ANULAR_CONTRATO",
            new { CONTRATO_ID = contratoId, MOTIVO = motivo, USUARIO_ID = usuarioId }, cancelacion)));
    }
}
