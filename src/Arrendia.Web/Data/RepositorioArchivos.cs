using Dapper;

namespace Arrendia.Web.Data;

public sealed class ArchivoInfo
{
    public int Id { get; set; }
    public string PublicId { get; set; } = "";
    public string NombreOriginal { get; set; } = "";
    public string TipoMime { get; set; } = "";
    public int TamanoBytes { get; set; }
    public string Categoria { get; set; } = "";
    public DateTime FechaCreacion { get; set; }
}

public sealed record NuevoArchivo(
    string PublicId,
    string NombreOriginal,
    string TipoMime,
    long TamanoBytes,
    string Categoria,
    int? ContratoId = null,
    int? ReporteId = null,
    int? CargoId = null,
    int? PagoId = null,
    int? UsuarioId = null,
    int? EnlaceId = null);

public interface IRepositorioArchivos
{
    Task<int> RegistrarAsync(NuevoArchivo archivo, CancellationToken cancelacion);
    Task<ArchivoInfo?> ObtenerSiPuedeVerAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<ArchivoInfo>> DeReporteAsync(int reporteId, CancellationToken cancelacion);
}

public sealed class RepositorioArchivos(IBaseDatos baseDatos) : IRepositorioArchivos
{
    public async Task<int> RegistrarAsync(NuevoArchivo archivo, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleAsync<int>(new CommandDefinition(
            """
            INSERT INTO dbo.ARCHIVO (PUBLIC_ID, NOMBRE_ORIGINAL, TIPO_MIME, TAMANO_BYTES, CATEGORIA, CONTRATO_ID, REPORTE_ID, CARGO_ID, PAGO_ID,
                                     SUBIDO_POR_USUARIO_ID, SUBIDO_POR_ENLACE_ID)
            OUTPUT INSERTED.ID
            VALUES (@PublicId, LEFT(@NombreOriginal, 200), @TipoMime, @TamanoBytes, @Categoria, @ContratoId, @ReporteId, @CargoId, @PagoId,
                    @UsuarioId, @EnlaceId)
            """,
            new
            {
                archivo.PublicId,
                archivo.NombreOriginal,
                archivo.TipoMime,
                TamanoBytes = (int)archivo.TamanoBytes,
                archivo.Categoria,
                archivo.ContratoId,
                archivo.ReporteId,
                archivo.CargoId,
                archivo.PagoId,
                archivo.UsuarioId,
                archivo.EnlaceId,
            },
            cancellationToken: cancelacion));
    }

    public async Task<ArchivoInfo?> ObtenerSiPuedeVerAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<ArchivoInfo>(new CommandDefinition(
            """
            SELECT a.ID, a.PUBLIC_ID, a.NOMBRE_ORIGINAL, a.TIPO_MIME, a.TAMANO_BYTES, a.CATEGORIA, a.FECHA_CREACION
            FROM dbo.ARCHIVO a
            WHERE a.ID = @Id AND (
                   a.SUBIDO_POR_USUARIO_ID = @UsuarioId
                OR (a.CONTRATO_ID IS NOT NULL AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, a.CONTRATO_ID) = 1)
                OR EXISTS (SELECT 1 FROM dbo.PAGO p WHERE p.ID = a.PAGO_ID AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, p.CONTRATO_ID) = 1)
                OR EXISTS (SELECT 1 FROM dbo.CARGO c WHERE c.ID = a.CARGO_ID AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, c.CONTRATO_ID) = 1)
                OR EXISTS (SELECT 1 FROM dbo.REPORTE_MANTENIMIENTO r WHERE r.ID = a.REPORTE_ID
                           AND (dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, r.PROPIEDAD_ID) = 1
                                OR (r.CONTRATO_ID IS NOT NULL AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, r.CONTRATO_ID) = 1))))
            """,
            new { Id = id, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<ArchivoInfo>> DeReporteAsync(int reporteId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<ArchivoInfo>(new CommandDefinition(
            """
            SELECT ID, PUBLIC_ID, NOMBRE_ORIGINAL, TIPO_MIME, TAMANO_BYTES, CATEGORIA, FECHA_CREACION
            FROM dbo.ARCHIVO WHERE REPORTE_ID = @ReporteId AND CATEGORIA IN ('FotoReporte', 'FotoTrabajo')
            ORDER BY FECHA_CREACION
            """,
            new { ReporteId = reporteId }, cancellationToken: cancelacion));
        return filas.AsList();
    }
}
