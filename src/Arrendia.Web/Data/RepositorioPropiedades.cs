using Dapper;
using Microsoft.Data.SqlClient;

namespace Arrendia.Web.Data;

public sealed class PropiedadResumen
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string Municipio { get; set; } = "";
    public string Departamento { get; set; } = "";
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public int Unidades { get; set; }
    public int Ocupadas { get; set; }
    public int Disponibles { get; set; }
    public int EnMantenimiento { get; set; }
    public int Inactivas { get; set; }
    public int CargosVencidos { get; set; }

    public int Ocupacion => Unidades - Inactivas == 0 ? 0 : (int)Math.Round(100.0 * Ocupadas / (Unidades - Inactivas));
}

public sealed class UnidadDetalle
{
    public int Id { get; set; }
    public int PropiedadId { get; set; }
    public string Propiedad { get; set; } = "";
    public string Codigo { get; set; } = "";
    public int TipoUnidadId { get; set; }
    public string TipoUnidad { get; set; } = "";
    public string? Nivel { get; set; }
    public decimal? AreaM2 { get; set; }
    public decimal? RentaReferencia { get; set; }
    public bool AplicaIva { get; set; }
    public string Estado { get; set; } = "";
    public int? ContratoId { get; set; }
    public string? ContratoNumero { get; set; }
    public string? Inquilino { get; set; }
    public bool ConAtraso { get; set; }
}

public sealed class Opcion
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

public sealed record DatosPropiedad(string Nombre, string Tipo, string Direccion, string Municipio, string Departamento, decimal? Latitud, decimal? Longitud);

public sealed record DatosUnidad(int PropiedadId, string Codigo, int TipoUnidadId, string? Nivel, decimal? AreaM2, decimal? RentaReferencia, bool AplicaIva);

public sealed record FiltroUnidades(int? PropiedadId = null, string? Estado = null, int? TipoUnidadId = null, string? Buscar = null);

public interface IRepositorioPropiedades
{
    Task<IReadOnlyList<PropiedadResumen>> ListarAsync(int usuarioId, CancellationToken cancelacion);
    Task<PropiedadResumen?> ObtenerAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<UnidadDetalle>> UnidadesAsync(int usuarioId, FiltroUnidades filtro, CancellationToken cancelacion);
    Task<UnidadDetalle?> UnidadAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<Opcion>> TiposDeUnidadAsync(CancellationToken cancelacion);
    Task<int> CrearAsync(DatosPropiedad datos, int usuarioId, CancellationToken cancelacion);
    Task<int> CrearConUnidadAsync(DatosPropiedad datos, DatosUnidad unidad, int usuarioId, CancellationToken cancelacion);
    Task ActualizarAsync(int id, DatosPropiedad datos, int usuarioId, CancellationToken cancelacion);
    Task<int> CrearUnidadAsync(DatosUnidad datos, int usuarioId, CancellationToken cancelacion);
    Task ActualizarUnidadAsync(int id, DatosUnidad datos, int usuarioId, CancellationToken cancelacion);
    Task CambiarEstadoUnidadAsync(int id, string estado, int usuarioId, CancellationToken cancelacion);
}

public sealed class RepositorioPropiedades(IBaseDatos baseDatos) : IRepositorioPropiedades
{
    public static readonly string[] EstadosManuales = ["Disponible", "Mantenimiento", "Inactiva"];

    private const string ConsultaResumen = """
        SELECT p.ID, p.NOMBRE, p.TIPO, p.DIRECCION, p.MUNICIPIO, p.DEPARTAMENTO, p.LATITUD, p.LONGITUD,
               COUNT(u.ID) AS UNIDADES,
               SUM(CASE WHEN u.ESTADO = 'Ocupada' THEN 1 ELSE 0 END) AS OCUPADAS,
               SUM(CASE WHEN u.ESTADO = 'Disponible' THEN 1 ELSE 0 END) AS DISPONIBLES,
               SUM(CASE WHEN u.ESTADO = 'Mantenimiento' THEN 1 ELSE 0 END) AS EN_MANTENIMIENTO,
               SUM(CASE WHEN u.ESTADO = 'Inactiva' THEN 1 ELSE 0 END) AS INACTIVAS,
               (SELECT COUNT(*) FROM dbo.CARGO k
                  JOIN dbo.CONTRATO c ON c.ID = k.CONTRATO_ID
                  JOIN dbo.UNIDAD x ON x.ID = c.UNIDAD_ID
                 WHERE x.PROPIEDAD_ID = p.ID AND k.SALDO > 0 AND k.ESTADO IN ('Pendiente', 'Parcial')
                   AND k.FECHA_VENCIMIENTO < dbo.FN_HOY_SV()) AS CARGOS_VENCIDOS
        FROM dbo.PROPIEDAD p
        LEFT JOIN dbo.UNIDAD u ON u.PROPIEDAD_ID = p.ID
        WHERE p.ACTIVO = 1 AND dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, p.ID) = 1
        """;

    private const string ConsultaUnidades = """
        SELECT u.ID, u.PROPIEDAD_ID, p.NOMBRE AS PROPIEDAD, u.CODIGO, u.TIPO_UNIDAD_ID, t.NOMBRE AS TIPO_UNIDAD, u.NIVEL,
               u.AREA_M2, u.RENTA_REFERENCIA, u.APLICA_IVA, u.ESTADO,
               c.ID AS CONTRATO_ID, c.NUMERO AS CONTRATO_NUMERO, i.NOMBRE AS INQUILINO,
               CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CARGO k WHERE k.CONTRATO_ID = c.ID AND k.SALDO > 0
                         AND k.ESTADO IN ('Pendiente', 'Parcial') AND k.FECHA_VENCIMIENTO < dbo.FN_HOY_SV())
                    THEN 1 ELSE 0 END AS BIT) AS CON_ATRASO
        FROM dbo.UNIDAD u
        JOIN dbo.PROPIEDAD p ON p.ID = u.PROPIEDAD_ID
        JOIN dbo.TIPO_UNIDAD t ON t.ID = u.TIPO_UNIDAD_ID
        LEFT JOIN dbo.CONTRATO c ON c.UNIDAD_ID = u.ID AND c.ESTADO = 'Activo'
        LEFT JOIN dbo.INQUILINO i ON i.ID = c.INQUILINO_ID
        WHERE p.ACTIVO = 1 AND dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, u.PROPIEDAD_ID) = 1
        """;

    public async Task<IReadOnlyList<PropiedadResumen>> ListarAsync(int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<PropiedadResumen>(new CommandDefinition(
            ConsultaResumen + Agrupar + " ORDER BY p.NOMBRE", new { UsuarioId = usuarioId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<PropiedadResumen?> ObtenerAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<PropiedadResumen>(new CommandDefinition(
            ConsultaResumen + " AND p.ID = @Id" + Agrupar, new { UsuarioId = usuarioId, Id = id }, cancellationToken: cancelacion));
    }

    private const string Agrupar = " GROUP BY p.ID, p.NOMBRE, p.TIPO, p.DIRECCION, p.MUNICIPIO, p.DEPARTAMENTO, p.LATITUD, p.LONGITUD";

    public async Task<IReadOnlyList<UnidadDetalle>> UnidadesAsync(int usuarioId, FiltroUnidades filtro, CancellationToken cancelacion)
    {
        var buscar = string.IsNullOrWhiteSpace(filtro.Buscar) ? null : $"%{filtro.Buscar.Trim().Replace("[", "[[]", StringComparison.Ordinal).Replace("%", "[%]", StringComparison.Ordinal).Replace("_", "[_]", StringComparison.Ordinal)}%";
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<UnidadDetalle>(new CommandDefinition(
            ConsultaUnidades + """
                 AND (@PropiedadId IS NULL OR u.PROPIEDAD_ID = @PropiedadId)
                 AND (@Estado IS NULL OR u.ESTADO = @Estado)
                 AND (@TipoUnidadId IS NULL OR u.TIPO_UNIDAD_ID = @TipoUnidadId)
                 AND (@Buscar IS NULL OR u.CODIGO LIKE @Buscar OR p.NOMBRE LIKE @Buscar OR i.NOMBRE LIKE @Buscar)
                """,
            new { UsuarioId = usuarioId, filtro.PropiedadId, filtro.Estado, filtro.TipoUnidadId, Buscar = buscar },
            cancellationToken: cancelacion));
        return [.. Models.Catalogos.Ordenar(filas)];
    }

    public async Task<UnidadDetalle?> UnidadAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<UnidadDetalle>(new CommandDefinition(
            ConsultaUnidades + " AND u.ID = @Id", new { UsuarioId = usuarioId, Id = id }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<Opcion>> TiposDeUnidadAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Opcion>(new CommandDefinition(
            "SELECT ID, NOMBRE FROM dbo.TIPO_UNIDAD WHERE ACTIVO = 1 ORDER BY NOMBRE", cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<int> CrearAsync(DatosPropiedad datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var id = await ConUnicoAsync("Ya existe una propiedad con ese nombre.", () => conexion.QuerySingleAsync<int>(new CommandDefinition(
            """
            INSERT INTO dbo.PROPIEDAD (NOMBRE, TIPO, DIRECCION, MUNICIPIO, DEPARTAMENTO, LATITUD, LONGITUD, CREADO_POR)
            OUTPUT INSERTED.ID
            VALUES (@Nombre, @Tipo, @Direccion, @Municipio, @Departamento, @Latitud, @Longitud, @UsuarioId);
            """,
            new { datos.Nombre, datos.Tipo, datos.Direccion, datos.Municipio, datos.Departamento, datos.Latitud, datos.Longitud, UsuarioId = usuarioId },
            cancellationToken: cancelacion)));
        await RegistrarAsync(conexion, usuarioId, "Crear", "PROPIEDAD", id, datos.Nombre, cancelacion);
        return id;
    }

    public async Task<int> CrearConUnidadAsync(DatosPropiedad datos, DatosUnidad unidad, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await using var transaccion = await conexion.BeginTransactionAsync(cancelacion);
        var id = await ConUnicoAsync("Ya existe una propiedad con ese nombre.", () => conexion.QuerySingleAsync<int>(new CommandDefinition(
            """
            INSERT INTO dbo.PROPIEDAD (NOMBRE, TIPO, DIRECCION, MUNICIPIO, DEPARTAMENTO, LATITUD, LONGITUD, CREADO_POR)
            OUTPUT INSERTED.ID
            VALUES (@Nombre, @Tipo, @Direccion, @Municipio, @Departamento, @Latitud, @Longitud, @UsuarioId);
            """,
            new { datos.Nombre, datos.Tipo, datos.Direccion, datos.Municipio, datos.Departamento, datos.Latitud, datos.Longitud, UsuarioId = usuarioId },
            transaccion, cancellationToken: cancelacion)));
        var unidadId = await conexion.QuerySingleAsync<int>(new CommandDefinition(
            """
            INSERT INTO dbo.UNIDAD (PROPIEDAD_ID, CODIGO, TIPO_UNIDAD_ID, NIVEL, AREA_M2, RENTA_REFERENCIA, APLICA_IVA, CREADO_POR)
            OUTPUT INSERTED.ID
            VALUES (@PropiedadId, @Codigo, @TipoUnidadId, @Nivel, @AreaM2, @RentaReferencia, @AplicaIva, @UsuarioId);
            """,
            new { PropiedadId = id, unidad.Codigo, unidad.TipoUnidadId, unidad.Nivel, unidad.AreaM2, unidad.RentaReferencia, unidad.AplicaIva, UsuarioId = usuarioId },
            transaccion, cancellationToken: cancelacion));
        await RegistrarAsync(conexion, usuarioId, "Crear", "PROPIEDAD", id, datos.Nombre, cancelacion, transaccion);
        await RegistrarAsync(conexion, usuarioId, "Crear", "UNIDAD", unidadId, unidad.Codigo, cancelacion, transaccion);
        await transaccion.CommitAsync(cancelacion);
        return id;
    }

    public async Task ActualizarAsync(int id, DatosPropiedad datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await ConUnicoAsync("Ya existe una propiedad con ese nombre.", () => conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.PROPIEDAD
            SET NOMBRE = @Nombre, TIPO = @Tipo, DIRECCION = @Direccion, MUNICIPIO = @Municipio, DEPARTAMENTO = @Departamento,
                LATITUD = @Latitud, LONGITUD = @Longitud, MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE ID = @Id;
            """,
            new { Id = id, datos.Nombre, datos.Tipo, datos.Direccion, datos.Municipio, datos.Departamento, datos.Latitud, datos.Longitud, UsuarioId = usuarioId },
            cancellationToken: cancelacion)));
        await RegistrarAsync(conexion, usuarioId, "Modificar", "PROPIEDAD", id, datos.Nombre, cancelacion);
    }

    public async Task<int> CrearUnidadAsync(DatosUnidad datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var id = await ConUnicoAsync("Ya existe una unidad con ese código en esta propiedad.", () => conexion.QuerySingleAsync<int>(new CommandDefinition(
            """
            INSERT INTO dbo.UNIDAD (PROPIEDAD_ID, CODIGO, TIPO_UNIDAD_ID, NIVEL, AREA_M2, RENTA_REFERENCIA, APLICA_IVA, CREADO_POR)
            OUTPUT INSERTED.ID
            VALUES (@PropiedadId, @Codigo, @TipoUnidadId, @Nivel, @AreaM2, @RentaReferencia, @AplicaIva, @UsuarioId);
            """,
            new { datos.PropiedadId, datos.Codigo, datos.TipoUnidadId, datos.Nivel, datos.AreaM2, datos.RentaReferencia, datos.AplicaIva, UsuarioId = usuarioId },
            cancellationToken: cancelacion)));
        await RegistrarAsync(conexion, usuarioId, "Crear", "UNIDAD", id, datos.Codigo, cancelacion);
        return id;
    }

    public async Task ActualizarUnidadAsync(int id, DatosUnidad datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await ConUnicoAsync("Ya existe una unidad con ese código en esta propiedad.", () => conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.UNIDAD
            SET CODIGO = @Codigo, TIPO_UNIDAD_ID = @TipoUnidadId, NIVEL = @Nivel, AREA_M2 = @AreaM2, RENTA_REFERENCIA = @RentaReferencia,
                APLICA_IVA = @AplicaIva, MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE ID = @Id;
            """,
            new { Id = id, datos.Codigo, datos.TipoUnidadId, datos.Nivel, datos.AreaM2, datos.RentaReferencia, datos.AplicaIva, UsuarioId = usuarioId },
            cancellationToken: cancelacion)));
        await RegistrarAsync(conexion, usuarioId, "Modificar", "UNIDAD", id, datos.Codigo, cancelacion);
    }

    public async Task CambiarEstadoUnidadAsync(int id, string estado, int usuarioId, CancellationToken cancelacion)
    {
        if (!EstadosManuales.Contains(estado))
        {
            throw new ReglaDeNegocioException("Ese estado no se puede asignar a mano: una unidad pasa a Ocupada al activar su contrato.", 0);
        }

        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await EjecucionSegura.ConReglasAsync(() => conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.UNIDAD SET ESTADO = @Estado, MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE ID = @Id AND ESTADO <> @Estado;
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID, DETALLE)
            VALUES (@UsuarioId, 'CambiarEstado', 'UNIDAD', @Id, (SELECT @Estado AS estado FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """,
            new { Id = id, Estado = estado, UsuarioId = usuarioId }, cancellationToken: cancelacion)));
    }

    private static Task<int> RegistrarAsync(SqlConnection conexion, int usuarioId, string accion, string tabla, int id, string nombre, CancellationToken cancelacion, System.Data.Common.DbTransaction? transaccion = null) =>
        conexion.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID, DETALLE)
            VALUES (@UsuarioId, @Accion, @Tabla, @Id, (SELECT @Nombre AS nombre FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """,
            new { UsuarioId = usuarioId, Accion = accion, Tabla = tabla, Id = id, Nombre = nombre }, transaccion, cancellationToken: cancelacion));

    private static async Task<T> ConUnicoAsync<T>(string mensaje, Func<Task<T>> accion)
    {
        try
        {
            return await accion();
        }
        catch (SqlException error) when (error.Number is 2627 or 2601)
        {
            throw new ReglaDeNegocioException(mensaje, error.Number);
        }
    }
}
