using Dapper;

namespace Arrendia.Web.Data;

public sealed class Notificacion
{
    public string Tipo { get; set; } = "";
    public int Id { get; set; }
    public string Numero { get; set; } = "";
    public string Detalle { get; set; } = "";
    public decimal? Monto { get; set; }
    public DateTime Fecha { get; set; }
    public int PropiedadId { get; set; }

    public string Ruta => Tipo == "Pago" ? $"/pagos/{Id}" : $"/reportes/{Id}";

    public string Titulo => Tipo switch
    {
        "Reporte" => $"Nuevo reporte {Numero}",
        "Terminado" => $"El técnico terminó {Numero}",
        _ => $"Pago en línea {Numero}",
    };
}

public sealed record Destinatario(string Nombre, string Email);

public interface IRepositorioNotificaciones
{
    Task<IReadOnlyList<Notificacion>> RecientesAsync(int usuarioId, CancellationToken cancelacion);
    Task<Notificacion?> BuscarAsync(string tipo, int? id, string? numero, CancellationToken cancelacion);
    Task<IReadOnlyList<Destinatario>> DestinatariosAsync(int propiedadId, CancellationToken cancelacion);
}

public sealed class RepositorioNotificaciones(IBaseDatos baseDatos) : IRepositorioNotificaciones
{
    public const int Dias = 14;

    private const string Eventos = """
        SELECT 'Reporte' AS TIPO, r.ID, r.NUMERO, r.TITULO AS DETALLE, CAST(NULL AS DECIMAL(12, 2)) AS MONTO, r.FECHA_CREACION AS FECHA, r.PROPIEDAD_ID
        FROM dbo.REPORTE_MANTENIMIENTO r JOIN dbo.USUARIO c ON c.ID = r.CREADO_POR
        WHERE c.ROL = 'Inquilino' AND r.FECHA_CREACION >= @Desde
        UNION ALL
        SELECT 'Terminado', r.ID, r.NUMERO, r.TITULO, NULL, h.FECHA_CREACION, r.PROPIEDAD_ID
        FROM dbo.REPORTE_HISTORIAL h JOIN dbo.REPORTE_MANTENIMIENTO r ON r.ID = h.REPORTE_ID
        WHERE h.ESTADO_NUEVO = 'Terminado' AND h.ENLACE_TECNICO_ID IS NOT NULL AND h.FECHA_CREACION >= @Desde
        UNION ALL
        SELECT 'Pago', p.ID, p.NUMERO, i.NOMBRE, p.MONTO, p.FECHA_CREACION, u.PROPIEDAD_ID
        FROM dbo.PAGO p
        JOIN dbo.CONTRATO k ON k.ID = p.CONTRATO_ID
        JOIN dbo.UNIDAD u ON u.ID = k.UNIDAD_ID
        JOIN dbo.INQUILINO i ON i.ID = p.INQUILINO_ID
        WHERE p.METODO = 'Tarjeta' AND p.ESTADO = 'Aplicado' AND p.FECHA_CREACION >= @Desde
        """;

    public async Task<IReadOnlyList<Notificacion>> RecientesAsync(int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Notificacion>(new CommandDefinition(
            $"""
            SELECT TOP (10) x.* FROM ({Eventos}) x
            WHERE dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, x.PROPIEDAD_ID) = 1
            ORDER BY x.FECHA DESC
            """,
            new { UsuarioId = usuarioId, Desde = DateTime.UtcNow.AddDays(-Dias) }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<Notificacion?> BuscarAsync(string tipo, int? id, string? numero, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QueryFirstOrDefaultAsync<Notificacion>(new CommandDefinition(
            $"""
            SELECT TOP (1) x.* FROM ({Eventos}) x
            WHERE x.TIPO = @Tipo AND (@Id IS NULL OR x.ID = @Id) AND (@Numero IS NULL OR x.NUMERO = @Numero)
            ORDER BY x.FECHA DESC
            """,
            new { Tipo = tipo, Id = id, Numero = numero, Desde = DateTime.UtcNow.AddDays(-1) }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<Destinatario>> DestinatariosAsync(int propiedadId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Destinatario>(new CommandDefinition(
            """
            SELECT u.NOMBRE, u.EMAIL FROM dbo.USUARIO u
            WHERE u.ACTIVO = 1 AND u.ROL IN ('Administrador', 'Supervisor') AND u.EMAIL IS NOT NULL
              AND dbo.FN_ACCESO_PROPIEDAD(u.ID, @PropiedadId) = 1
            """,
            new { PropiedadId = propiedadId }, cancellationToken: cancelacion));
        return filas.AsList();
    }
}
