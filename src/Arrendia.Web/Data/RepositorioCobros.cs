using System.Data;
using Arrendia.Web.Services;
using Dapper;

namespace Arrendia.Web.Data;

public sealed class AjusteFila
{
    public string Tipo { get; set; } = "";
    public decimal Monto { get; set; }
    public string Motivo { get; set; } = "";
    public string Autorizo { get; set; } = "";
    public DateTime FechaCreacion { get; set; }
}

public sealed class AplicacionFila
{
    public int PagoId { get; set; }
    public string Pago { get; set; } = "";
    public int CargoId { get; set; }
    public string Descripcion { get; set; } = "";
    public decimal MontoAplicado { get; set; }
    public decimal SaldoActual { get; set; }
    public bool Revertida { get; set; }
    public DateTime Fecha { get; set; }
}

public sealed class PagoDetalle
{
    public int Id { get; set; }
    public string Numero { get; set; } = "";
    public int ContratoId { get; set; }
    public string Contrato { get; set; } = "";
    public string Inquilino { get; set; } = "";
    public string Unidad { get; set; } = "";
    public string Propiedad { get; set; } = "";
    public decimal Monto { get; set; }
    public DateTime FechaPago { get; set; }
    public string Metodo { get; set; } = "";
    public string? Referencia { get; set; }
    public string Estado { get; set; } = "";
    public int? ArchivoComprobanteId { get; set; }
    public int? ComprobanteDocumentoId { get; set; }
    public string? FolioComprobante { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string Registro { get; set; } = "";
}

public sealed class CobranzaFila
{
    public int ContratoId { get; set; }
    public string Numero { get; set; } = "";
    public string Inquilino { get; set; } = "";
    public string Unidad { get; set; } = "";
    public int PropiedadId { get; set; }
    public string Propiedad { get; set; } = "";
    public decimal PorVencer { get; set; }
    public decimal De1A30 { get; set; }
    public decimal De31A60 { get; set; }
    public decimal De61A90 { get; set; }
    public decimal MasDe90 { get; set; }
    public decimal MoraPendiente { get; set; }
    public int MaxDiasAtraso { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    public decimal Vencido => De1A30 + De31A60 + De61A90 + MasDe90;
}

public sealed record FiltroCargos(string? Situacion = null, int? PropiedadId = null, int? ContratoId = null, string? Origen = null, string? Buscar = null);

public sealed record FiltroPagos(string? Metodo = null, string? Estado = null, int? PropiedadId = null, DateTime? Desde = null, DateTime? Hasta = null, string? Buscar = null);

public sealed record NuevoCargo(int ContratoId, string ConceptoCodigo, string Descripcion, decimal MontoBase, DateTime FechaVencimiento, bool? GeneraMora, int? ArchivoId);

public sealed record NuevoPago(int ContratoId, decimal Monto, string Metodo, string? Referencia, IReadOnlyCollection<int>? Cargos, int? ArchivoComprobanteId);

public interface IRepositorioCobros
{
    Task<IReadOnlyList<CargoFila>> CargosAsync(int usuarioId, FiltroCargos filtro, CancellationToken cancelacion);
    Task<CargoFila?> CargoAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<AjusteFila>> AjustesAsync(int cargoId, CancellationToken cancelacion);
    Task<IReadOnlyList<AplicacionFila>> AplicacionesDeCargoAsync(int cargoId, CancellationToken cancelacion);
    Task<int?> ArchivoRespaldoAsync(int cargoId, CancellationToken cancelacion);
    Task<IReadOnlyList<Concepto>> ConceptosManualesAsync(CancellationToken cancelacion);
    Task<int> CrearCargoAsync(NuevoCargo datos, int usuarioId, CancellationToken cancelacion);
    Task CondonarAsync(int cargoId, decimal monto, string motivo, int usuarioId, CancellationToken cancelacion);
    Task AnularCargoAsync(int cargoId, string motivo, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<CargoPendiente>> PendientesAsync(int contratoId, CancellationToken cancelacion);
    Task<bool> PermiteParcialesAsync(int contratoId, CancellationToken cancelacion);
    Task<IReadOnlyList<PagoFila>> PagosAsync(int usuarioId, FiltroPagos filtro, CancellationToken cancelacion);
    Task<PagoDetalle?> PagoAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<AplicacionFila>> AplicacionesDePagoAsync(int pagoId, CancellationToken cancelacion);
    Task<(int Id, string Numero)> RegistrarPagoAsync(NuevoPago datos, int usuarioId, CancellationToken cancelacion);
    Task AnularPagoAsync(int pagoId, string motivo, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<CobranzaFila>> CobranzaAsync(int usuarioId, int? propiedadId, int minimoDias, CancellationToken cancelacion);
}

public sealed class RepositorioCobros(IBaseDatos baseDatos) : IRepositorioCobros
{
    public async Task<IReadOnlyList<CargoFila>> CargosAsync(int usuarioId, FiltroCargos filtro, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<CargoFila>(new CommandDefinition(
            """
            SELECT TOP (500) * FROM dbo.VW_ESTADO_CUENTA
            WHERE dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, PROPIEDAD_ID) = 1
              AND (@Situacion IS NULL OR SITUACION = @Situacion OR (@Situacion = 'Pendientes' AND SITUACION IN ('Vencido', 'Por vencer')))
              AND (@PropiedadId IS NULL OR PROPIEDAD_ID = @PropiedadId)
              AND (@ContratoId IS NULL OR CONTRATO_ID = @ContratoId)
              AND (@Origen IS NULL OR ORIGEN = @Origen)
              AND (@Buscar IS NULL OR DESCRIPCION LIKE @Buscar OR CONTRATO LIKE @Buscar OR INQUILINO LIKE @Buscar OR UNIDAD LIKE @Buscar)
            ORDER BY CASE SITUACION WHEN 'Vencido' THEN 0 WHEN 'Por vencer' THEN 1 ELSE 2 END, FECHA_VENCIMIENTO, CARGO_ID
            """,
            new { UsuarioId = usuarioId, filtro.Situacion, filtro.PropiedadId, filtro.ContratoId, filtro.Origen, Buscar = Texto.ComoPatron(filtro.Buscar) },
            cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<CargoFila?> CargoAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<CargoFila>(new CommandDefinition(
            "SELECT * FROM dbo.VW_ESTADO_CUENTA WHERE CARGO_ID = @Id AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, CONTRATO_ID) = 1",
            new { Id = id, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<AjusteFila>> AjustesAsync(int cargoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<AjusteFila>(new CommandDefinition(
            """
            SELECT a.TIPO, a.MONTO, a.MOTIVO, u.NOMBRE AS AUTORIZO, a.FECHA_CREACION
            FROM dbo.AJUSTE_CUENTA a JOIN dbo.USUARIO u ON u.ID = a.AUTORIZADO_POR WHERE a.CARGO_ID = @Id ORDER BY a.ID
            """,
            new { Id = cargoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<AplicacionFila>> AplicacionesDeCargoAsync(int cargoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<AplicacionFila>(new CommandDefinition(
            """
            SELECT a.PAGO_ID, p.NUMERO AS PAGO, a.CARGO_ID, k.DESCRIPCION, a.MONTO_APLICADO, k.SALDO AS SALDO_ACTUAL, a.REVERTIDA,
                   dbo.FN_A_HORA_SV(p.FECHA_PAGO) AS FECHA
            FROM dbo.APLICACION_PAGO a JOIN dbo.PAGO p ON p.ID = a.PAGO_ID JOIN dbo.CARGO k ON k.ID = a.CARGO_ID
            WHERE a.CARGO_ID = @Id ORDER BY p.FECHA_PAGO
            """,
            new { Id = cargoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<int?> ArchivoRespaldoAsync(int cargoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT ARCHIVO_RESPALDO_ID FROM dbo.CARGO WHERE ID = @Id", new { Id = cargoId }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<Concepto>> ConceptosManualesAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Concepto>(new CommandDefinition(
            "SELECT ID, CODIGO, NOMBRE, ES_SISTEMA, APLICA_IVA, GENERA_MORA_POR_DEFECTO, ACTIVO FROM dbo.CONCEPTO_CARGO WHERE ES_SISTEMA = 0 AND ACTIVO = 1 ORDER BY NOMBRE",
            cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<int> CrearCargoAsync(NuevoCargo datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var p = new DynamicParameters(new
        {
            CONTRATO_ID = datos.ContratoId,
            CONCEPTO_CODIGO = datos.ConceptoCodigo,
            DESCRIPCION = datos.Descripcion,
            MONTO_BASE = datos.MontoBase,
            FECHA_VENCIMIENTO = datos.FechaVencimiento.Date,
            GENERA_MORA = datos.GeneraMora,
            ARCHIVO_RESPALDO_ID = datos.ArchivoId,
            USUARIO_ID = usuarioId,
        });
        p.Add("CARGO_ID", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CREAR_CARGO_MANUAL", p, cancelacion)));
        return p.Get<int>("CARGO_ID");
    }

    public async Task CondonarAsync(int cargoId, decimal monto, string motivo, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_CONDONAR_CARGO",
            new { CARGO_ID = cargoId, MONTO = monto, MOTIVO = motivo, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task AnularCargoAsync(int cargoId, string motivo, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_ANULAR_CARGO",
            new { CARGO_ID = cargoId, MOTIVO = motivo, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task<IReadOnlyList<CargoPendiente>> PendientesAsync(int contratoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<(int Id, string Descripcion, decimal Saldo, DateTime FechaBase, bool EsMora)>(new CommandDefinition(
            """
            SELECT c.ID, c.DESCRIPCION, c.SALDO, COALESCE(b.FECHA_VENCIMIENTO, c.FECHA_VENCIMIENTO) AS FECHA_BASE,
                   CAST(CASE WHEN c.ORIGEN = 'Mora' THEN 1 ELSE 0 END AS BIT) AS ES_MORA
            FROM dbo.CARGO c LEFT JOIN dbo.CARGO b ON b.ID = c.CARGO_ORIGEN_ID
            WHERE c.CONTRATO_ID = @Id AND c.SALDO > 0 AND c.ESTADO IN ('Pendiente', 'Parcial')
            ORDER BY FECHA_BASE, ES_MORA DESC, c.ID
            """,
            new { Id = contratoId }, cancellationToken: cancelacion));
        return [.. filas.Select(f => new CargoPendiente(f.Id, f.Descripcion, f.Saldo, f.FechaBase, f.EsMora))];
    }

    public async Task<bool> PermiteParcialesAsync(int contratoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<bool?>(new CommandDefinition(
            "SELECT TOP (1) PERMITE_PAGOS_PARCIALES FROM dbo.FN_CONDICION_VIGENTE(@Id, dbo.FN_HOY_SV())",
            new { Id = contratoId }, cancellationToken: cancelacion)) ?? true;
    }

    public async Task<IReadOnlyList<PagoFila>> PagosAsync(int usuarioId, FiltroPagos filtro, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<PagoFila>(new CommandDefinition(
            """
            SELECT TOP (500) * FROM dbo.VW_HISTORIAL_PAGOS
            WHERE dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, PROPIEDAD_ID) = 1
              AND (@Metodo IS NULL OR METODO = @Metodo)
              AND (@Estado IS NULL OR ESTADO = @Estado)
              AND (@PropiedadId IS NULL OR PROPIEDAD_ID = @PropiedadId)
              AND (@Desde IS NULL OR CAST(FECHA_LOCAL AS DATE) >= @Desde)
              AND (@Hasta IS NULL OR CAST(FECHA_LOCAL AS DATE) <= @Hasta)
              AND (@Buscar IS NULL OR NUMERO LIKE @Buscar OR CONTRATO LIKE @Buscar OR INQUILINO LIKE @Buscar OR REFERENCIA LIKE @Buscar)
            ORDER BY FECHA_LOCAL DESC
            """,
            new { UsuarioId = usuarioId, filtro.Metodo, filtro.Estado, filtro.PropiedadId, Desde = filtro.Desde?.Date, Hasta = filtro.Hasta?.Date, Buscar = Texto.ComoPatron(filtro.Buscar) },
            cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<PagoDetalle?> PagoAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<PagoDetalle>(new CommandDefinition(
            """
            SELECT p.ID, p.NUMERO, p.CONTRATO_ID, c.NUMERO AS CONTRATO, i.NOMBRE AS INQUILINO, u.CODIGO AS UNIDAD, pr.NOMBRE AS PROPIEDAD,
                   p.MONTO, p.FECHA_PAGO, p.METODO, p.REFERENCIA, p.ESTADO, p.ARCHIVO_COMPROBANTE_ID, p.COMPROBANTE_DOCUMENTO_ID, d.FOLIO AS FOLIO_COMPROBANTE,
                   p.MOTIVO_ANULACION, p.FECHA_ANULACION, r.NOMBRE AS REGISTRO
            FROM dbo.PAGO p
            JOIN dbo.CONTRATO c ON c.ID = p.CONTRATO_ID
            JOIN dbo.INQUILINO i ON i.ID = p.INQUILINO_ID
            JOIN dbo.UNIDAD u ON u.ID = c.UNIDAD_ID
            JOIN dbo.PROPIEDAD pr ON pr.ID = u.PROPIEDAD_ID
            JOIN dbo.USUARIO r ON r.ID = p.CREADO_POR
            LEFT JOIN dbo.DOCUMENTO_GENERADO d ON d.ID = p.COMPROBANTE_DOCUMENTO_ID
            WHERE p.ID = @Id AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, p.CONTRATO_ID) = 1
            """,
            new { Id = id, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<AplicacionFila>> AplicacionesDePagoAsync(int pagoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<AplicacionFila>(new CommandDefinition(
            """
            SELECT a.PAGO_ID, p.NUMERO AS PAGO, a.CARGO_ID, k.DESCRIPCION, a.MONTO_APLICADO, k.SALDO AS SALDO_ACTUAL, a.REVERTIDA,
                   dbo.FN_A_HORA_SV(p.FECHA_PAGO) AS FECHA
            FROM dbo.APLICACION_PAGO a JOIN dbo.PAGO p ON p.ID = a.PAGO_ID JOIN dbo.CARGO k ON k.ID = a.CARGO_ID
            WHERE a.PAGO_ID = @Id ORDER BY a.ORDEN
            """,
            new { Id = pagoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<(int Id, string Numero)> RegistrarPagoAsync(NuevoPago datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var p = new DynamicParameters(new
        {
            CONTRATO_ID = datos.ContratoId,
            MONTO = datos.Monto,
            METODO = datos.Metodo,
            REFERENCIA = datos.Referencia,
            CARGOS = datos.Cargos is { Count: > 0 } c ? System.Text.Json.JsonSerializer.Serialize(c) : null,
            ARCHIVO_COMPROBANTE_ID = datos.ArchivoComprobanteId,
            USUARIO_ID = usuarioId,
        });
        p.Add("PAGO_ID", dbType: DbType.Int32, direction: ParameterDirection.Output);
        p.Add("NUMERO", dbType: DbType.AnsiString, size: 20, direction: ParameterDirection.Output);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_REGISTRAR_PAGO", p, cancelacion)));
        return (p.Get<int>("PAGO_ID"), p.Get<string>("NUMERO"));
    }

    public async Task AnularPagoAsync(int pagoId, string motivo, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_ANULAR_PAGO",
            new { PAGO_ID = pagoId, MOTIVO = motivo, USUARIO_ID = usuarioId }, cancelacion)));
    }

    public async Task<IReadOnlyList<CobranzaFila>> CobranzaAsync(int usuarioId, int? propiedadId, int minimoDias, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<CobranzaFila>(new CommandDefinition(
            """
            SELECT v.CONTRATO_ID, v.NUMERO, v.INQUILINO, v.UNIDAD, v.PROPIEDAD_ID, p.NOMBRE AS PROPIEDAD, v.POR_VENCER,
                   v.DE_1_A_30 AS DE1_A30, v.DE_31_A_60 AS DE31_A60, v.DE_61_A_90 AS DE61_A90, v.MAS_DE_90, v.MORA_PENDIENTE, v.MAX_DIAS_ATRASO,
                   i.TELEFONO,
                   (SELECT TOP (1) k.EMAIL FROM dbo.CONTACTO_INQUILINO k WHERE k.INQUILINO_ID = c.INQUILINO_ID AND k.ACTIVO = 1 ORDER BY k.ES_PRINCIPAL DESC) AS EMAIL
            FROM dbo.VW_COBRANZA v
            JOIN dbo.PROPIEDAD p ON p.ID = v.PROPIEDAD_ID
            JOIN dbo.CONTRATO c ON c.ID = v.CONTRATO_ID
            JOIN dbo.INQUILINO i ON i.ID = c.INQUILINO_ID
            WHERE dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, v.PROPIEDAD_ID) = 1
              AND (@PropiedadId IS NULL OR v.PROPIEDAD_ID = @PropiedadId)
              AND v.MAX_DIAS_ATRASO >= @Minimo
            ORDER BY v.MAX_DIAS_ATRASO DESC, (v.DE_1_A_30 + v.DE_31_A_60 + v.DE_61_A_90 + v.MAS_DE_90) DESC
            """,
            new { UsuarioId = usuarioId, PropiedadId = propiedadId, Minimo = minimoDias }, cancellationToken: cancelacion));
        return filas.AsList();
    }
}
