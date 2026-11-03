using System.Data;
using Arrendia.Web.Services;
using Dapper;

namespace Arrendia.Web.Data;

public sealed class Empresa
{
    public string Nombre { get; set; } = "";
    public string Nit { get; set; } = "";
    public decimal PorcentajeIva { get; set; }
}

public sealed class DocumentoFila
{
    public int Id { get; set; }
    public string Folio { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string CodigoVerificacion { get; set; } = "";
    public bool Anulado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int ArchivoId { get; set; }
    public int? ContratoId { get; set; }
    public string? Contrato { get; set; }
    public int? PagoId { get; set; }
    public string? Pago { get; set; }
    public string? Inquilino { get; set; }
}

public sealed class DocumentoVerificado
{
    public string Folio { get; set; } = "";
    public string Tipo { get; set; } = "";
    public bool Anulado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public byte[] HashSha256 { get; set; } = [];
    public string? Contrato { get; set; }
    public string? Pago { get; set; }
    public decimal? MontoPago { get; set; }
    public string? EstadoPago { get; set; }
    public string? Inquilino { get; set; }
}

public sealed record FiltroDocumentos(string? Tipo = null, string? Buscar = null, int? ContratoId = null);

public interface IRepositorioDocumentos
{
    Task<string> ReservarFolioAsync(CancellationToken cancelacion);
    Task<int> RegistrarAsync(string folio, string tipo, string codigo, byte[] hash, string publicId, string nombre, int tamano,
        int? contratoId, int? pagoId, int usuarioId, CancellationToken cancelacion);
    Task<int> IdSistemaAsync(CancellationToken cancelacion);
    Task<Empresa> EmpresaAsync(CancellationToken cancelacion);
    Task<DatosContratoPdf?> DatosContratoAsync(int contratoId, CancellationToken cancelacion);
    Task<DatosComprobantePdf?> DatosComprobanteAsync(int pagoId, CancellationToken cancelacion);
    Task<DatosAvisoPdf?> DatosAvisoAsync(int cargoId, CancellationToken cancelacion);
    Task<EstadoCuenta> EstadoCuentaAsync(int contratoId, DateTime desde, DateTime hasta, int usuarioId, CancellationToken cancelacion);
    Task<int?> ComprobanteDePagoAsync(int pagoId, CancellationToken cancelacion);
    Task<IReadOnlyList<DocumentoFila>> ListarAsync(int usuarioId, FiltroDocumentos filtro, CancellationToken cancelacion);
    Task<DocumentoFila?> ObtenerAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<DocumentoVerificado?> VerificarAsync(string codigo, CancellationToken cancelacion);
    Task<ArchivoInfo?> ArchivoAsync(int documentoId, CancellationToken cancelacion);
}

public sealed class RepositorioDocumentos(IBaseDatos baseDatos) : IRepositorioDocumentos
{
    private const string ConsultaDocumentos = """
        SELECT d.ID, d.FOLIO, d.TIPO, d.CODIGO_VERIFICACION, d.ANULADO, d.FECHA_CREACION, d.ARCHIVO_ID,
               COALESCE(d.CONTRATO_ID, p.CONTRATO_ID) AS CONTRATO_ID, c.NUMERO AS CONTRATO, d.PAGO_ID, p.NUMERO AS PAGO, i.NOMBRE AS INQUILINO
        FROM dbo.DOCUMENTO_GENERADO d
        LEFT JOIN dbo.PAGO p ON p.ID = d.PAGO_ID
        LEFT JOIN dbo.CONTRATO c ON c.ID = COALESCE(d.CONTRATO_ID, p.CONTRATO_ID)
        LEFT JOIN dbo.INQUILINO i ON i.ID = c.INQUILINO_ID
        WHERE c.ID IS NOT NULL AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, c.ID) = 1
        """;

    public async Task<string> ReservarFolioAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var p = new DynamicParameters();
        p.Add("FOLIO", dbType: DbType.AnsiString, size: 20, direction: ParameterDirection.Output);
        await conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_RESERVAR_FOLIO", p, cancelacion));
        return p.Get<string>("FOLIO");
    }

    public async Task<int> RegistrarAsync(string folio, string tipo, string codigo, byte[] hash, string publicId, string nombre, int tamano,
        int? contratoId, int? pagoId, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var p = new DynamicParameters(new
        {
            FOLIO = folio,
            TIPO = tipo,
            CODIGO_VERIFICACION = codigo,
            HASH_SHA256 = hash,
            PUBLIC_ID = publicId,
            NOMBRE_ORIGINAL = nombre,
            TAMANO_BYTES = tamano,
            CONTRATO_ID = contratoId,
            PAGO_ID = pagoId,
            USUARIO_ID = usuarioId,
        });
        p.Add("DOCUMENTO_ID", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(EjecucionSegura.Procedimiento("dbo.SP_REGISTRAR_DOCUMENTO", p, cancelacion)));
        return p.Get<int>("DOCUMENTO_ID");
    }

    public async Task<int> IdSistemaAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleAsync<int>(new CommandDefinition("SELECT dbo.FN_ID_SISTEMA()", cancellationToken: cancelacion));
    }

    public async Task<Empresa> EmpresaAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleAsync<Empresa>(new CommandDefinition(
            """
            SELECT ISNULL((SELECT VALOR FROM dbo.PARAMETRO WHERE CLAVE = 'Empresa.Nombre'), N'Arrendia') AS NOMBRE,
                   ISNULL((SELECT VALOR FROM dbo.PARAMETRO WHERE CLAVE = 'Empresa.NIT'), N'') AS NIT,
                   CAST(dbo.FN_PARAMETRO_NUMERO('IVA.Porcentaje', 13) AS DECIMAL(9,4)) AS PORCENTAJE_IVA
            """,
            cancellationToken: cancelacion));
    }

    public async Task<DatosContratoPdf?> DatosContratoAsync(int contratoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<DatosContratoPdf>(new CommandDefinition(
            """
            SELECT c.NUMERO, c.FECHA_INICIO, c.FECHA_FIN, c.MONTO_DEPOSITO,
                   i.NOMBRE AS INQUILINO, i.TIPO_PERSONA, i.DUI, i.NIT, i.NRC, i.REPRESENTANTE_LEGAL, i.DIRECCION_FISCAL, i.TELEFONO,
                   u.CODIGO AS UNIDAD, t.NOMBRE AS TIPO_UNIDAD, u.NIVEL, u.AREA_M2, u.APLICA_IVA,
                   p.NOMBRE AS PROPIEDAD, p.DIRECCION, p.MUNICIPIO, p.DEPARTAMENTO,
                   k.RENTA, k.DIA_PAGO, k.DIAS_GRACIA, k.APLICA_MORA, k.PORCENTAJE_MORA_DIARIO, k.PERMITE_PAGOS_PARCIALES,
                   CAST(dbo.FN_PARAMETRO_NUMERO('IVA.Porcentaje', 13) AS DECIMAL(9,4)) AS PORCENTAJE_IVA
            FROM dbo.CONTRATO c
            JOIN dbo.INQUILINO i ON i.ID = c.INQUILINO_ID
            JOIN dbo.UNIDAD u ON u.ID = c.UNIDAD_ID
            JOIN dbo.TIPO_UNIDAD t ON t.ID = u.TIPO_UNIDAD_ID
            JOIN dbo.PROPIEDAD p ON p.ID = u.PROPIEDAD_ID
            CROSS APPLY (SELECT TOP (1) * FROM dbo.CONDICION_CONTRATO x WHERE x.CONTRATO_ID = c.ID ORDER BY
                         CASE WHEN x.VIGENTE_DESDE <= dbo.FN_HOY_SV() THEN 0 ELSE 1 END, x.NUMERO_VERSION DESC) k
            WHERE c.ID = @Id
            """,
            new { Id = contratoId }, cancellationToken: cancelacion));
    }

    public async Task<DatosComprobantePdf?> DatosComprobanteAsync(int pagoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var datos = await conexion.QuerySingleOrDefaultAsync<DatosComprobantePdf>(new CommandDefinition(
            """
            SELECT p.NUMERO, p.FECHA_PAGO, p.METODO, p.REFERENCIA, p.MONTO, p.ESTADO,
                   c.NUMERO AS CONTRATO, i.NOMBRE AS INQUILINO, u.CODIGO AS UNIDAD, pr.NOMBRE AS PROPIEDAD
            FROM dbo.PAGO p
            JOIN dbo.CONTRATO c ON c.ID = p.CONTRATO_ID
            JOIN dbo.INQUILINO i ON i.ID = p.INQUILINO_ID
            JOIN dbo.UNIDAD u ON u.ID = c.UNIDAD_ID
            JOIN dbo.PROPIEDAD pr ON pr.ID = u.PROPIEDAD_ID
            WHERE p.ID = @Id
            """,
            new { Id = pagoId }, cancellationToken: cancelacion));
        if (datos is null)
        {
            return null;
        }

        var lineas = await conexion.QueryAsync<LineaPdf>(new CommandDefinition(
            """
            SELECT k.DESCRIPCION, a.MONTO_APLICADO AS MONTO, k.SALDO
            FROM dbo.APLICACION_PAGO a JOIN dbo.CARGO k ON k.ID = a.CARGO_ID
            WHERE a.PAGO_ID = @Id ORDER BY a.ORDEN
            """,
            new { Id = pagoId }, cancellationToken: cancelacion));
        datos.Aplicaciones = lineas.AsList();
        return datos;
    }

    public async Task<DatosAvisoPdf?> DatosAvisoAsync(int cargoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<DatosAvisoPdf>(new CommandDefinition(
            """
            SELECT k.DESCRIPCION, k.PERIODO, k.FECHA_EMISION, k.FECHA_VENCIMIENTO, k.MONTO_BASE, k.MONTO_IVA, k.MONTO, k.SALDO,
                   c.NUMERO AS CONTRATO, i.NOMBRE AS INQUILINO, u.CODIGO AS UNIDAD, p.NOMBRE AS PROPIEDAD,
                   dbo.FN_DEUDA_CONTRATO(c.ID) AS DEUDA_TOTAL
            FROM dbo.CARGO k
            JOIN dbo.CONTRATO c ON c.ID = k.CONTRATO_ID
            JOIN dbo.INQUILINO i ON i.ID = c.INQUILINO_ID
            JOIN dbo.UNIDAD u ON u.ID = c.UNIDAD_ID
            JOIN dbo.PROPIEDAD p ON p.ID = u.PROPIEDAD_ID
            WHERE k.ID = @Id
            """,
            new { Id = cargoId }, cancellationToken: cancelacion));
    }

    public async Task<EstadoCuenta> EstadoCuentaAsync(int contratoId, DateTime desde, DateTime hasta, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        using var lector = await EjecucionSegura.ConReglasAsync(() => conexion.QueryMultipleAsync(EjecucionSegura.Procedimiento(
            "dbo.SP_ESTADO_CUENTA", new { CONTRATO_ID = contratoId, DESDE = desde.Date, HASTA = hasta.Date, USUARIO_ID = usuarioId }, cancelacion)));
        var estado = await lector.ReadSingleAsync<EstadoCuenta>();
        estado.Movimientos = (await lector.ReadAsync<MovimientoCuenta>()).AsList();
        estado.Desde = desde.Date;
        estado.Hasta = hasta.Date;
        var cabecera = await conexion.QuerySingleAsync<(string Contrato, string Inquilino, string Unidad, string Propiedad)>(new CommandDefinition(
            """
            SELECT c.NUMERO, i.NOMBRE, u.CODIGO, p.NOMBRE
            FROM dbo.CONTRATO c JOIN dbo.INQUILINO i ON i.ID = c.INQUILINO_ID JOIN dbo.UNIDAD u ON u.ID = c.UNIDAD_ID JOIN dbo.PROPIEDAD p ON p.ID = u.PROPIEDAD_ID
            WHERE c.ID = @Id
            """,
            new { Id = contratoId }, cancellationToken: cancelacion));
        (estado.Contrato, estado.Inquilino, estado.Unidad, estado.Propiedad) = cabecera;
        return estado;
    }

    public async Task<int?> ComprobanteDePagoAsync(int pagoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT COMPROBANTE_DOCUMENTO_ID FROM dbo.PAGO WHERE ID = @Id", new { Id = pagoId }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<DocumentoFila>> ListarAsync(int usuarioId, FiltroDocumentos filtro, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<DocumentoFila>(new CommandDefinition(
            $"""
            SELECT TOP (300) * FROM ({ConsultaDocumentos}) x
            WHERE (@Tipo IS NULL OR x.TIPO = @Tipo)
              AND (@ContratoId IS NULL OR x.CONTRATO_ID = @ContratoId)
              AND (@Buscar IS NULL OR x.FOLIO LIKE @Buscar OR x.CONTRATO LIKE @Buscar OR x.PAGO LIKE @Buscar OR x.INQUILINO LIKE @Buscar)
            ORDER BY x.ID DESC
            """,
            new { UsuarioId = usuarioId, filtro.Tipo, filtro.ContratoId, Buscar = Texto.ComoPatron(filtro.Buscar) },
            cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<DocumentoFila?> ObtenerAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<DocumentoFila>(new CommandDefinition(
            $"SELECT * FROM ({ConsultaDocumentos}) x WHERE x.ID = @Id",
            new { UsuarioId = usuarioId, Id = id }, cancellationToken: cancelacion));
    }

    public async Task<ArchivoInfo?> ArchivoAsync(int documentoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<ArchivoInfo>(new CommandDefinition(
            ConsultasArchivo.DeDocumento, new { Id = documentoId }, cancellationToken: cancelacion));
    }

    public async Task<DocumentoVerificado?> VerificarAsync(string codigo, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<DocumentoVerificado>(new CommandDefinition(
            """
            SELECT d.FOLIO, d.TIPO, d.ANULADO, d.FECHA_CREACION, d.HASH_SHA256, c.NUMERO AS CONTRATO, p.NUMERO AS PAGO,
                   p.MONTO AS MONTO_PAGO, p.ESTADO AS ESTADO_PAGO, i.NOMBRE AS INQUILINO
            FROM dbo.DOCUMENTO_GENERADO d
            LEFT JOIN dbo.PAGO p ON p.ID = d.PAGO_ID
            LEFT JOIN dbo.CONTRATO c ON c.ID = COALESCE(d.CONTRATO_ID, p.CONTRATO_ID)
            LEFT JOIN dbo.INQUILINO i ON i.ID = c.INQUILINO_ID
            WHERE d.CODIGO_VERIFICACION = @Codigo
            """,
            new { Codigo = new DbString { Value = codigo, IsAnsi = true, IsFixedLength = true, Length = 12 } }, cancellationToken: cancelacion));
    }
}

public static class ConsultasArchivo
{
    public const string DeDocumento = """
        SELECT a.ID, a.PUBLIC_ID, a.NOMBRE_ORIGINAL, a.TIPO_MIME, a.TAMANO_BYTES, a.CATEGORIA, a.FECHA_CREACION
        FROM dbo.DOCUMENTO_GENERADO d JOIN dbo.ARCHIVO a ON a.ID = d.ARCHIVO_ID WHERE d.ID = @Id
        """;
}

public static class Texto
{
    public static string? ComoPatron(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? null
            : $"%{texto.Trim().Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal)}%";
}
