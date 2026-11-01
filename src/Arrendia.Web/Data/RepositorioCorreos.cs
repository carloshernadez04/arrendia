using Dapper;

namespace Arrendia.Web.Data;

public sealed class CorreoCola
{
    public long Id { get; set; }
    public string Tipo { get; set; } = "";
    public string Destinatario { get; set; } = "";
    public string Asunto { get; set; } = "";
    public string Estado { get; set; } = "";
    public int Intentos { get; set; }
    public string? Error { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int? UsuarioId { get; set; }
    public int? ContratoId { get; set; }
    public string? Contrato { get; set; }
    public int? CargoId { get; set; }
    public int? PagoId { get; set; }
    public int? DocumentoGeneradoId { get; set; }
}

public sealed record FiltroCorreos(string? Estado = null, string? Tipo = null, string? Buscar = null, int Pagina = 1);

public interface IRepositorioCorreos
{
    Task<long?> PendienteAsync(string tipo, int? usuarioId, string? destinatario, CancellationToken cancelacion);
    Task MarcarAsync(long id, bool enviado, string? mensajeError, CancellationToken cancelacion);
    Task<IReadOnlyList<CorreoCola>> ParaEnviarAsync(int lote, CancellationToken cancelacion);
    Task<int> ExpirarSinEnlaceAsync(CancellationToken cancelacion);
    Task<int?> DocumentoDeCargoAsync(int cargoId, CancellationToken cancelacion);
    Task VincularDocumentoAsync(long correoId, int documentoId, CancellationToken cancelacion);
    Task<ArchivoInfo?> ArchivoDeDocumentoAsync(int documentoId, CancellationToken cancelacion);
    Task<(IReadOnlyList<CorreoCola> Filas, int Total)> ListarAsync(FiltroCorreos filtro, int tamano, CancellationToken cancelacion);
    Task<bool> ReintentarAsync(long id, CancellationToken cancelacion);
}

public sealed class RepositorioCorreos(IBaseDatos baseDatos) : IRepositorioCorreos
{
    public async Task<long?> PendienteAsync(string tipo, int? usuarioId, string? destinatario, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            """
            SELECT TOP (1) ID FROM dbo.CORREO_ENVIADO
            WHERE TIPO = @Tipo AND ESTADO = 'Pendiente'
              AND (@UsuarioId IS NULL OR USUARIO_ID = @UsuarioId)
              AND (@Destinatario IS NULL OR DESTINATARIO = @Destinatario)
            ORDER BY ID DESC
            """,
            new { Tipo = tipo, UsuarioId = usuarioId, Destinatario = destinatario }, cancellationToken: cancelacion));
    }

    public async Task MarcarAsync(long id, bool enviado, string? mensajeError, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.CORREO_ENVIADO
            SET ESTADO = CASE WHEN @Enviado = 1 THEN 'Enviado' ELSE 'Fallido' END, INTENTOS = INTENTOS + 1,
                FECHA_ENVIO = CASE WHEN @Enviado = 1 THEN SYSUTCDATETIME() ELSE NULL END, ERROR = LEFT(@Error, 1000)
            WHERE ID = @Id
            """,
            new { Id = id, Enviado = enviado, Error = mensajeError }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<CorreoCola>> ParaEnviarAsync(int lote, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<CorreoCola>(new CommandDefinition(
            """
            SELECT TOP (@Lote) ID, TIPO, DESTINATARIO, ASUNTO, ESTADO, INTENTOS, ERROR, FECHA_ENVIO, FECHA_CREACION,
                   USUARIO_ID, CONTRATO_ID, CARGO_ID, PAGO_ID, DOCUMENTO_GENERADO_ID
            FROM dbo.CORREO_ENVIADO
            WHERE ESTADO = 'Pendiente' AND TIPO IN ('AvisoCobro', 'Comprobante') AND INTENTOS < 5
            ORDER BY ID
            """,
            new { Lote = lote }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<int> ExpirarSinEnlaceAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.CORREO_ENVIADO
            SET ESTADO = 'Fallido', ERROR = N'No se envió al momento de crearlo. Genera un enlace nuevo desde la pantalla correspondiente.'
            WHERE ESTADO = 'Pendiente' AND TIPO IN ('Invitacion', 'Recuperacion', 'CambioCorreo', 'EnlaceTecnico')
              AND FECHA_CREACION < DATEADD(MINUTE, -30, SYSUTCDATETIME())
            """,
            cancellationToken: cancelacion));
    }

    public async Task<int?> DocumentoDeCargoAsync(int cargoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT TOP (1) DOCUMENTO_GENERADO_ID FROM dbo.CORREO_ENVIADO WHERE CARGO_ID = @Id AND DOCUMENTO_GENERADO_ID IS NOT NULL ORDER BY ID",
            new { Id = cargoId }, cancellationToken: cancelacion));
    }

    public async Task VincularDocumentoAsync(long correoId, int documentoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.CORREO_ENVIADO SET DOCUMENTO_GENERADO_ID = @Documento WHERE ID = @Id",
            new { Id = correoId, Documento = documentoId }, cancellationToken: cancelacion));
    }

    public async Task<ArchivoInfo?> ArchivoDeDocumentoAsync(int documentoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<ArchivoInfo>(new CommandDefinition(
            ConsultasArchivo.DeDocumento,
            new { Id = documentoId }, cancellationToken: cancelacion));
    }

    public async Task<(IReadOnlyList<CorreoCola> Filas, int Total)> ListarAsync(FiltroCorreos filtro, int tamano, CancellationToken cancelacion)
    {
        const string Condicion = """
            WHERE (@Estado IS NULL OR c.ESTADO = @Estado) AND (@Tipo IS NULL OR c.TIPO = @Tipo)
              AND (@Buscar IS NULL OR c.DESTINATARIO LIKE @Buscar OR c.ASUNTO LIKE @Buscar)
            """;
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var parametros = new
        {
            filtro.Estado,
            filtro.Tipo,
            Buscar = Texto.ComoPatron(filtro.Buscar),
            Saltar = (Math.Max(filtro.Pagina, 1) - 1) * tamano,
            Tamano = tamano,
        };
        using var lector = await conexion.QueryMultipleAsync(new CommandDefinition(
            $"""
            SELECT COUNT(*) FROM dbo.CORREO_ENVIADO c {Condicion};
            SELECT c.ID, c.TIPO, c.DESTINATARIO, c.ASUNTO, c.ESTADO, c.INTENTOS, c.ERROR, c.FECHA_ENVIO, c.FECHA_CREACION,
                   c.USUARIO_ID, c.CONTRATO_ID, k.NUMERO AS CONTRATO, c.CARGO_ID, c.PAGO_ID, c.DOCUMENTO_GENERADO_ID
            FROM dbo.CORREO_ENVIADO c LEFT JOIN dbo.CONTRATO k ON k.ID = c.CONTRATO_ID
            {Condicion}
            ORDER BY c.ID DESC OFFSET @Saltar ROWS FETCH NEXT @Tamano ROWS ONLY;
            """,
            parametros, cancellationToken: cancelacion));
        var total = await lector.ReadSingleAsync<int>();
        var filas = (await lector.ReadAsync<CorreoCola>()).AsList();
        return (filas, total);
    }

    public async Task<bool> ReintentarAsync(long id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.CORREO_ENVIADO SET ESTADO = 'Pendiente', INTENTOS = 0, ERROR = NULL
            WHERE ID = @Id AND ESTADO = 'Fallido' AND TIPO IN ('AvisoCobro', 'Comprobante')
            """,
            new { Id = id }, cancellationToken: cancelacion)) > 0;
    }
}
