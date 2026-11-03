using Dapper;
using Microsoft.Data.SqlClient;

namespace Arrendia.Web.Data;

public sealed class Plantilla
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool EsPredeterminada { get; set; }
    public int DiaPago { get; set; } = 5;
    public int DiasEmisionAviso { get; set; } = 5;
    public bool AplicaMora { get; set; } = true;
    public int DiasGracia { get; set; } = 5;
    public decimal PorcentajeMoraDiario { get; set; }
    public string TipoTopeMora { get; set; } = "Ninguno";
    public decimal? ValorTopeMora { get; set; }
    public bool PermitePagosParciales { get; set; } = true;
    public bool CorrerVencimientoFeriado { get; set; } = true;
    public bool Activo { get; set; } = true;
}

public sealed class Concepto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public bool EsSistema { get; set; }
    public string AplicaIva { get; set; } = "Si";
    public bool GeneraMoraPorDefecto { get; set; }
    public bool Activo { get; set; } = true;
}

public sealed class ElementoCatalogo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; }
    public int? EspecialidadId { get; set; }
    public string? Especialidad { get; set; }
    public int HorasAtencion { get; set; }
    public int Usos { get; set; }
}

public sealed class Feriado
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Nombre { get; set; } = "";
}

public sealed class Parametro
{
    public int Id { get; set; }
    public string Clave { get; set; } = "";
    public string Valor { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string? Descripcion { get; set; }
}

public interface IRepositorioConfiguracion
{
    Task<IReadOnlyList<Plantilla>> PlantillasAsync(bool soloActivas, CancellationToken cancelacion);
    Task<Plantilla?> PlantillaAsync(int id, CancellationToken cancelacion);
    Task<int> GuardarPlantillaAsync(Plantilla plantilla, int usuarioId, CancellationToken cancelacion);
    Task HacerPredeterminadaAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<Concepto>> ConceptosAsync(CancellationToken cancelacion);
    Task GuardarConceptoAsync(Concepto concepto, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<ElementoCatalogo>> CatalogoAsync(string tabla, CancellationToken cancelacion);
    Task GuardarCatalogoAsync(string tabla, ElementoCatalogo elemento, CancellationToken cancelacion);
    Task<IReadOnlyList<Feriado>> FeriadosAsync(CancellationToken cancelacion);
    Task AgregarFeriadoAsync(DateTime fecha, string nombre, CancellationToken cancelacion);
    Task QuitarFeriadoAsync(int id, CancellationToken cancelacion);
    Task<IReadOnlyList<Parametro>> ParametrosAsync(CancellationToken cancelacion);
    Task GuardarParametroAsync(string clave, string valor, int usuarioId, CancellationToken cancelacion);
}

public sealed class RepositorioConfiguracion(IBaseDatos baseDatos) : IRepositorioConfiguracion
{
    public static readonly string[] Catalogos = ["TIPO_UNIDAD", "ESPECIALIDAD", "CATEGORIA_REPORTE"];

    private const string ColumnasPlantilla = """
        ID, NOMBRE, ES_PREDETERMINADA, DIA_PAGO, DIAS_EMISION_AVISO, APLICA_MORA, DIAS_GRACIA, PORCENTAJE_MORA_DIARIO,
        TIPO_TOPE_MORA, VALOR_TOPE_MORA, PERMITE_PAGOS_PARCIALES, CORRER_VENCIMIENTO_FERIADO, ACTIVO
        """;

    public async Task<IReadOnlyList<Plantilla>> PlantillasAsync(bool soloActivas, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Plantilla>(new CommandDefinition(
            $"SELECT {ColumnasPlantilla} FROM dbo.PLANTILLA_CONDICIONES WHERE @Solo = 0 OR ACTIVO = 1 ORDER BY ES_PREDETERMINADA DESC, NOMBRE",
            new { Solo = soloActivas }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<Plantilla?> PlantillaAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<Plantilla>(new CommandDefinition(
            $"SELECT {ColumnasPlantilla} FROM dbo.PLANTILLA_CONDICIONES WHERE ID = @Id", new { Id = id }, cancellationToken: cancelacion));
    }

    public async Task<int> GuardarPlantillaAsync(Plantilla plantilla, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var parametros = new
        {
            plantilla.Id,
            plantilla.Nombre,
            plantilla.DiaPago,
            plantilla.DiasEmisionAviso,
            plantilla.AplicaMora,
            plantilla.DiasGracia,
            plantilla.PorcentajeMoraDiario,
            plantilla.TipoTopeMora,
            ValorTopeMora = plantilla.TipoTopeMora == "Ninguno" ? null : plantilla.ValorTopeMora,
            plantilla.PermitePagosParciales,
            plantilla.CorrerVencimientoFeriado,
            Activo = plantilla.Activo || plantilla.EsPredeterminada,
            UsuarioId = usuarioId,
        };
        return await Unico("Ya existe una plantilla con ese nombre.", () => conexion.QuerySingleAsync<int>(new CommandDefinition(
            """
            IF @Id = 0
                INSERT INTO dbo.PLANTILLA_CONDICIONES (NOMBRE, DIA_PAGO, DIAS_EMISION_AVISO, APLICA_MORA, DIAS_GRACIA, PORCENTAJE_MORA_DIARIO,
                       TIPO_TOPE_MORA, VALOR_TOPE_MORA, PERMITE_PAGOS_PARCIALES, CORRER_VENCIMIENTO_FERIADO, ACTIVO, CREADO_POR)
                OUTPUT INSERTED.ID
                VALUES (@Nombre, @DiaPago, @DiasEmisionAviso, @AplicaMora, @DiasGracia, @PorcentajeMoraDiario, @TipoTopeMora, @ValorTopeMora,
                        @PermitePagosParciales, @CorrerVencimientoFeriado, @Activo, @UsuarioId);
            ELSE
                UPDATE dbo.PLANTILLA_CONDICIONES
                SET NOMBRE = @Nombre, DIA_PAGO = @DiaPago, DIAS_EMISION_AVISO = @DiasEmisionAviso, APLICA_MORA = @AplicaMora, DIAS_GRACIA = @DiasGracia,
                    PORCENTAJE_MORA_DIARIO = @PorcentajeMoraDiario, TIPO_TOPE_MORA = @TipoTopeMora, VALOR_TOPE_MORA = @ValorTopeMora,
                    PERMITE_PAGOS_PARCIALES = @PermitePagosParciales, CORRER_VENCIMIENTO_FERIADO = @CorrerVencimientoFeriado,
                    ACTIVO = CASE WHEN ES_PREDETERMINADA = 1 THEN 1 ELSE @Activo END,
                    MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
                OUTPUT INSERTED.ID
                WHERE ID = @Id;
            """,
            parametros, cancellationToken: cancelacion)));
    }

    public async Task HacerPredeterminadaAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await using var transaccion = await conexion.BeginTransactionAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.PLANTILLA_CONDICIONES SET ES_PREDETERMINADA = 0 WHERE ES_PREDETERMINADA = 1 AND ID <> @Id;
            UPDATE dbo.PLANTILLA_CONDICIONES SET ES_PREDETERMINADA = 1, ACTIVO = 1, MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME() WHERE ID = @Id;
            """,
            new { Id = id, UsuarioId = usuarioId }, transaccion, cancellationToken: cancelacion));
        await transaccion.CommitAsync(cancelacion);
    }

    public async Task<IReadOnlyList<Concepto>> ConceptosAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Concepto>(new CommandDefinition(
            "SELECT ID, CODIGO, NOMBRE, ES_SISTEMA, APLICA_IVA, GENERA_MORA_POR_DEFECTO, ACTIVO FROM dbo.CONCEPTO_CARGO ORDER BY ES_SISTEMA DESC, NOMBRE",
            cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task GuardarConceptoAsync(Concepto concepto, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await Unico("Ya existe un concepto con ese código.", () => conexion.ExecuteAsync(new CommandDefinition(
            """
            IF @Id = 0
                INSERT INTO dbo.CONCEPTO_CARGO (CODIGO, NOMBRE, APLICA_IVA, GENERA_MORA_POR_DEFECTO, CREADO_POR)
                VALUES (@Codigo, @Nombre, @AplicaIva, @GeneraMoraPorDefecto, @UsuarioId);
            ELSE
                UPDATE dbo.CONCEPTO_CARGO
                SET NOMBRE = @Nombre, APLICA_IVA = @AplicaIva, GENERA_MORA_POR_DEFECTO = @GeneraMoraPorDefecto, ACTIVO = @Activo,
                    MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
                WHERE ID = @Id AND ES_SISTEMA = 0;
            """,
            new { concepto.Id, concepto.Codigo, concepto.Nombre, concepto.AplicaIva, concepto.GeneraMoraPorDefecto, concepto.Activo, UsuarioId = usuarioId }, cancellationToken: cancelacion)));
    }

    public async Task<IReadOnlyList<ElementoCatalogo>> CatalogoAsync(string tabla, CancellationToken cancelacion)
    {
        var sql = tabla switch
        {
            "TIPO_UNIDAD" => "SELECT t.ID, t.NOMBRE, t.ACTIVO, (SELECT COUNT(*) FROM dbo.UNIDAD u WHERE u.TIPO_UNIDAD_ID = t.ID) AS USOS FROM dbo.TIPO_UNIDAD t ORDER BY t.NOMBRE",
            "ESPECIALIDAD" => "SELECT e.ID, e.NOMBRE, e.ACTIVO, (SELECT COUNT(*) FROM dbo.TECNICO_ESPECIALIDAD x WHERE x.ESPECIALIDAD_ID = e.ID) AS USOS FROM dbo.ESPECIALIDAD e ORDER BY e.NOMBRE",
            "CATEGORIA_REPORTE" => """
                SELECT c.ID, c.NOMBRE, c.ACTIVO, c.ESPECIALIDAD_ID, e.NOMBRE AS ESPECIALIDAD, c.HORAS_ATENCION,
                       (SELECT COUNT(*) FROM dbo.REPORTE_MANTENIMIENTO r WHERE r.CATEGORIA_REPORTE_ID = c.ID) AS USOS
                FROM dbo.CATEGORIA_REPORTE c LEFT JOIN dbo.ESPECIALIDAD e ON e.ID = c.ESPECIALIDAD_ID ORDER BY c.NOMBRE
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(tabla)),
        };
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<ElementoCatalogo>(new CommandDefinition(sql, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task GuardarCatalogoAsync(string tabla, ElementoCatalogo elemento, CancellationToken cancelacion)
    {
        var sql = tabla switch
        {
            "TIPO_UNIDAD" => """
                IF @Id = 0 INSERT INTO dbo.TIPO_UNIDAD (NOMBRE) VALUES (@Nombre);
                ELSE UPDATE dbo.TIPO_UNIDAD SET NOMBRE = @Nombre, ACTIVO = @Activo WHERE ID = @Id;
                """,
            "ESPECIALIDAD" => """
                IF @Id = 0 INSERT INTO dbo.ESPECIALIDAD (NOMBRE) VALUES (@Nombre);
                ELSE UPDATE dbo.ESPECIALIDAD SET NOMBRE = @Nombre, ACTIVO = @Activo WHERE ID = @Id;
                """,
            "CATEGORIA_REPORTE" => """
                IF @Id = 0 INSERT INTO dbo.CATEGORIA_REPORTE (NOMBRE, ESPECIALIDAD_ID, HORAS_ATENCION) VALUES (@Nombre, @EspecialidadId, @HorasAtencion);
                ELSE UPDATE dbo.CATEGORIA_REPORTE SET NOMBRE = @Nombre, ESPECIALIDAD_ID = @EspecialidadId, HORAS_ATENCION = @HorasAtencion, ACTIVO = @Activo WHERE ID = @Id;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(tabla)),
        };
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await Unico("Ya existe un elemento con ese nombre.", () => conexion.ExecuteAsync(new CommandDefinition(sql,
            new { elemento.Id, elemento.Nombre, elemento.Activo, elemento.EspecialidadId, HorasAtencion = Math.Clamp(elemento.HorasAtencion, 1, 2000) }, cancellationToken: cancelacion)));
    }

    public async Task<IReadOnlyList<Feriado>> FeriadosAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Feriado>(new CommandDefinition(
            "SELECT ID, FECHA, NOMBRE FROM dbo.FERIADO WHERE FECHA >= DATEADD(YEAR, -1, dbo.FN_HOY_SV()) ORDER BY FECHA", cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task AgregarFeriadoAsync(DateTime fecha, string nombre, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await Unico("Ya hay un feriado en esa fecha.", () => conexion.ExecuteAsync(new CommandDefinition(
            "INSERT INTO dbo.FERIADO (FECHA, NOMBRE) VALUES (@Fecha, @Nombre)", new { Fecha = fecha.Date, Nombre = nombre }, cancellationToken: cancelacion)));
    }

    public async Task QuitarFeriadoAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition("DELETE FROM dbo.FERIADO WHERE ID = @Id", new { Id = id }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<Parametro>> ParametrosAsync(CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Parametro>(new CommandDefinition(
            "SELECT ID, CLAVE, VALOR, TIPO, DESCRIPCION FROM dbo.PARAMETRO ORDER BY CLAVE", cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task GuardarParametroAsync(string clave, string valor, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.PARAMETRO SET VALOR = @Valor, MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME() WHERE CLAVE = @Clave;
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, DETALLE)
            VALUES (@UsuarioId, 'Modificar', 'PARAMETRO', (SELECT @Clave AS clave, @Valor AS valor FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """,
            new { Clave = clave, Valor = valor, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    private static async Task<T> Unico<T>(string mensaje, Func<Task<T>> accion)
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
