using Dapper;
using Microsoft.Data.SqlClient;

namespace Arrendia.Web.Data;

public sealed class InquilinoFila
{
    public int Id { get; set; }
    public string TipoPersona { get; set; } = "N";
    public string Nombre { get; set; } = "";
    public string? Dui { get; set; }
    public string? Nit { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string Estado { get; set; } = "";
    public string Acceso { get; set; } = "";
    public int ContratosActivos { get; set; }
    public decimal Deuda { get; set; }

    public string Documento => TipoPersona == "J" ? $"NIT {Nit}" : $"DUI {Dui}";
}

public sealed class InquilinoDetalle
{
    public int Id { get; set; }
    public string TipoPersona { get; set; } = "N";
    public string Nombre { get; set; } = "";
    public string? Dui { get; set; }
    public string? Nit { get; set; }
    public string? Nrc { get; set; }
    public string? RepresentanteLegal { get; set; }
    public string? Telefono { get; set; }
    public string? DireccionFiscal { get; set; }
    public string Estado { get; set; } = "";
    public DateTime FechaCreacion { get; set; }
    public int? UsuarioId { get; set; }
    public string? EmailPortal { get; set; }
    public string Acceso { get; set; } = "Sin acceso";
}

public sealed class ContactoFila
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefono { get; set; }
    public bool EsPrincipal { get; set; }
    public bool RecibeAvisos { get; set; }
    public bool Activo { get; set; }
}

public sealed class ContratoResumen
{
    public int ContratoId { get; set; }
    public string Numero { get; set; } = "";
    public string Estado { get; set; } = "";
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public decimal MontoDeposito { get; set; }
    public int UnidadId { get; set; }
    public string Unidad { get; set; } = "";
    public int PropiedadId { get; set; }
    public string Propiedad { get; set; } = "";
    public int InquilinoId { get; set; }
    public string Inquilino { get; set; } = "";
    public decimal? RentaVigente { get; set; }
    public int? DiaPago { get; set; }
    public decimal DeudaTotal { get; set; }
    public decimal DeudaVencida { get; set; }
    public DateTime? ProximoVencimiento { get; set; }
}

public sealed record DatosInquilino(string TipoPersona, string Nombre, string? Dui, string? Nit, string? Nrc, string? RepresentanteLegal,
    string? Telefono, string? DireccionFiscal);

public sealed record DatosContacto(string Nombre, string Email, string? Telefono, bool RecibeAvisos);

public sealed record FiltroInquilinos(string? Buscar = null, string? Estado = null, string? Acceso = null);

public interface IRepositorioInquilinos
{
    Task<IReadOnlyList<InquilinoFila>> ListarAsync(int usuarioId, FiltroInquilinos filtro, CancellationToken cancelacion);
    Task<IReadOnlyList<Opcion>> ActivosAsync(int usuarioId, CancellationToken cancelacion);
    Task<InquilinoDetalle?> ObtenerAsync(int id, int usuarioId, CancellationToken cancelacion);
    Task<IReadOnlyList<ContactoFila>> ContactosAsync(int inquilinoId, CancellationToken cancelacion);
    Task<IReadOnlyList<ContratoResumen>> ContratosAsync(int inquilinoId, int usuarioId, CancellationToken cancelacion);
    Task<int> CrearAsync(DatosInquilino datos, DatosContacto? contacto, int usuarioId, CancellationToken cancelacion);
    Task ActualizarAsync(int id, DatosInquilino datos, int usuarioId, CancellationToken cancelacion);
    Task<bool> TieneContratoActivoAsync(int id, CancellationToken cancelacion);
    Task CambiarEstadoAsync(int id, string estado, int usuarioId, CancellationToken cancelacion);
    Task<int> AgregarContactoAsync(int inquilinoId, DatosContacto datos, int usuarioId, CancellationToken cancelacion);
    Task ActualizarContactoAsync(int inquilinoId, int contactoId, DatosContacto datos, int usuarioId, CancellationToken cancelacion);
    Task HacerPrincipalAsync(int inquilinoId, int contactoId, int usuarioId, CancellationToken cancelacion);
    Task DesactivarContactoAsync(int inquilinoId, int contactoId, int usuarioId, CancellationToken cancelacion);
}

public sealed class RepositorioInquilinos(IBaseDatos baseDatos) : IRepositorioInquilinos
{
    public const string Visible = """
        (dbo.FN_TIENE_PERMISO(@UsuarioId, 'ADMIN') = 1 OR i.CREADO_POR = @UsuarioId
         OR EXISTS (SELECT 1 FROM dbo.CONTRATO c JOIN dbo.UNIDAD u ON u.ID = c.UNIDAD_ID
                    WHERE c.INQUILINO_ID = i.ID AND dbo.FN_ACCESO_PROPIEDAD(@UsuarioId, u.PROPIEDAD_ID) = 1))
        """;

    public async Task<IReadOnlyList<InquilinoFila>> ListarAsync(int usuarioId, FiltroInquilinos filtro, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<InquilinoFila>(new CommandDefinition(
            $"""
            SELECT TOP (500) i.ID, i.TIPO_PERSONA, i.NOMBRE, i.DUI, i.NIT, i.TELEFONO, i.ESTADO, a.ACCESO,
                   (SELECT TOP (1) k.EMAIL FROM dbo.CONTACTO_INQUILINO k WHERE k.INQUILINO_ID = i.ID AND k.ACTIVO = 1 ORDER BY k.ES_PRINCIPAL DESC, k.ID) AS EMAIL,
                   (SELECT COUNT(*) FROM dbo.CONTRATO c WHERE c.INQUILINO_ID = i.ID AND c.ESTADO = 'Activo') AS CONTRATOS_ACTIVOS,
                   ISNULL((SELECT SUM(k.SALDO) FROM dbo.CARGO k JOIN dbo.CONTRATO c ON c.ID = k.CONTRATO_ID
                           WHERE c.INQUILINO_ID = i.ID AND k.ESTADO IN ('Pendiente', 'Parcial')), 0) AS DEUDA
            FROM dbo.INQUILINO i
            JOIN dbo.VW_ACCESO_PORTAL a ON a.INQUILINO_ID = i.ID
            WHERE {Visible}
              AND (@Estado IS NULL OR i.ESTADO = @Estado)
              AND (@Acceso IS NULL OR a.ACCESO = @Acceso)
              AND (@Buscar IS NULL OR i.NOMBRE LIKE @Buscar OR i.DUI LIKE @Buscar OR i.NIT LIKE @Buscar OR i.TELEFONO LIKE @Buscar
                   OR EXISTS (SELECT 1 FROM dbo.CONTACTO_INQUILINO k WHERE k.INQUILINO_ID = i.ID AND k.EMAIL LIKE @Buscar))
            ORDER BY i.NOMBRE
            """,
            new { UsuarioId = usuarioId, filtro.Estado, filtro.Acceso, Buscar = Texto.ComoPatron(filtro.Buscar) },
            cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<Opcion>> ActivosAsync(int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<Opcion>(new CommandDefinition(
            $"""
            SELECT i.ID, i.NOMBRE + ' · ' + CASE WHEN i.TIPO_PERSONA = 'J' THEN 'NIT ' + i.NIT ELSE 'DUI ' + i.DUI END AS NOMBRE
            FROM dbo.INQUILINO i WHERE i.ESTADO = 'Activo' AND {Visible} ORDER BY i.NOMBRE
            """,
            new { UsuarioId = usuarioId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<InquilinoDetalle?> ObtenerAsync(int id, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleOrDefaultAsync<InquilinoDetalle>(new CommandDefinition(
            $"""
            SELECT i.ID, i.TIPO_PERSONA, i.NOMBRE, i.DUI, i.NIT, i.NRC, i.REPRESENTANTE_LEGAL, i.TELEFONO, i.DIRECCION_FISCAL, i.ESTADO,
                   i.FECHA_CREACION, a.USUARIO_ID, a.EMAIL AS EMAIL_PORTAL, a.ACCESO
            FROM dbo.INQUILINO i JOIN dbo.VW_ACCESO_PORTAL a ON a.INQUILINO_ID = i.ID
            WHERE i.ID = @Id AND {Visible}
            """,
            new { Id = id, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    public async Task<IReadOnlyList<ContactoFila>> ContactosAsync(int inquilinoId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<ContactoFila>(new CommandDefinition(
            """
            SELECT ID, NOMBRE, EMAIL, TELEFONO, ES_PRINCIPAL, RECIBE_AVISOS, ACTIVO
            FROM dbo.CONTACTO_INQUILINO WHERE INQUILINO_ID = @Id ORDER BY ACTIVO DESC, ES_PRINCIPAL DESC, NOMBRE
            """,
            new { Id = inquilinoId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<ContratoResumen>> ContratosAsync(int inquilinoId, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        var filas = await conexion.QueryAsync<ContratoResumen>(new CommandDefinition(
            """
            SELECT * FROM dbo.VW_RESUMEN_CONTRATO
            WHERE INQUILINO_ID = @Id AND dbo.FN_ACCESO_CONTRATO(@UsuarioId, CONTRATO_ID) = 1
            ORDER BY CASE ESTADO WHEN 'Activo' THEN 0 WHEN 'PendienteFirma' THEN 1 WHEN 'Borrador' THEN 2 ELSE 3 END, FECHA_INICIO DESC
            """,
            new { Id = inquilinoId, UsuarioId = usuarioId }, cancellationToken: cancelacion));
        return filas.AsList();
    }

    public async Task<int> CrearAsync(DatosInquilino datos, DatosContacto? contacto, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await using var transaccion = await conexion.BeginTransactionAsync(cancelacion);
        var id = await ConUnicoAsync(() => conexion.QuerySingleAsync<int>(new CommandDefinition(
            """
            INSERT INTO dbo.INQUILINO (TIPO_PERSONA, NOMBRE, DUI, NIT, NRC, REPRESENTANTE_LEGAL, TELEFONO, DIRECCION_FISCAL, CREADO_POR)
            OUTPUT INSERTED.ID
            VALUES (@TipoPersona, @Nombre, @Dui, @Nit, @Nrc, @RepresentanteLegal, @Telefono, @DireccionFiscal, @UsuarioId)
            """,
            Parametros(datos, usuarioId), transaccion, cancellationToken: cancelacion)));
        if (contacto is not null)
        {
            await conexion.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO dbo.CONTACTO_INQUILINO (INQUILINO_ID, NOMBRE, EMAIL, TELEFONO, ES_PRINCIPAL, RECIBE_AVISOS, CREADO_POR)
                VALUES (@InquilinoId, @Nombre, @Email, @Telefono, 1, @RecibeAvisos, @UsuarioId)
                """,
                new { InquilinoId = id, contacto.Nombre, contacto.Email, contacto.Telefono, contacto.RecibeAvisos, UsuarioId = usuarioId },
                transaccion, cancellationToken: cancelacion));
        }

        await Bitacora(conexion, transaccion, usuarioId, "Crear", id, datos.Nombre, cancelacion);
        await transaccion.CommitAsync(cancelacion);
        return id;
    }

    public async Task ActualizarAsync(int id, DatosInquilino datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await ConUnicoAsync(() => conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.INQUILINO
            SET TIPO_PERSONA = @TipoPersona, NOMBRE = @Nombre, DUI = @Dui, NIT = @Nit, NRC = @Nrc, REPRESENTANTE_LEGAL = @RepresentanteLegal,
                TELEFONO = @Telefono, DIRECCION_FISCAL = @DireccionFiscal, MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE ID = @Id
            """,
            new
            {
                Id = id,
                datos.TipoPersona,
                datos.Nombre,
                datos.Dui,
                datos.Nit,
                datos.Nrc,
                datos.RepresentanteLegal,
                datos.Telefono,
                datos.DireccionFiscal,
                UsuarioId = usuarioId,
            },
            cancellationToken: cancelacion)));
        await Bitacora(conexion, null, usuarioId, "Modificar", id, datos.Nombre, cancelacion);
    }

    public async Task<bool> TieneContratoActivoAsync(int id, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleAsync<bool>(new CommandDefinition(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.CONTRATO WHERE INQUILINO_ID = @Id AND ESTADO IN ('Activo', 'PendienteFirma', 'Borrador')) THEN 1 ELSE 0 END AS BIT)",
            new { Id = id }, cancellationToken: cancelacion));
    }

    public async Task CambiarEstadoAsync(int id, string estado, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.INQUILINO SET ESTADO = @Estado, MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME() WHERE ID = @Id;
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID, DETALLE)
            VALUES (@UsuarioId, 'CambiarEstado', 'INQUILINO', @Id, (SELECT @Estado AS estado FOR JSON PATH, WITHOUT_ARRAY_WRAPPER));
            """,
            new { Id = id, Estado = estado, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    public async Task<int> AgregarContactoAsync(int inquilinoId, DatosContacto datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        return await conexion.QuerySingleAsync<int>(new CommandDefinition(
            """
            INSERT INTO dbo.CONTACTO_INQUILINO (INQUILINO_ID, NOMBRE, EMAIL, TELEFONO, ES_PRINCIPAL, RECIBE_AVISOS, CREADO_POR)
            OUTPUT INSERTED.ID
            VALUES (@InquilinoId, @Nombre, @Email, @Telefono,
                    CASE WHEN EXISTS (SELECT 1 FROM dbo.CONTACTO_INQUILINO WHERE INQUILINO_ID = @InquilinoId AND ES_PRINCIPAL = 1) THEN 0 ELSE 1 END,
                    @RecibeAvisos, @UsuarioId)
            """,
            new { InquilinoId = inquilinoId, datos.Nombre, datos.Email, datos.Telefono, datos.RecibeAvisos, UsuarioId = usuarioId },
            cancellationToken: cancelacion));
    }

    public async Task ActualizarContactoAsync(int inquilinoId, int contactoId, DatosContacto datos, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.CONTACTO_INQUILINO
            SET NOMBRE = @Nombre, EMAIL = @Email, TELEFONO = @Telefono, RECIBE_AVISOS = @RecibeAvisos, EMAIL_VALIDO = 1,
                MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE ID = @Id AND INQUILINO_ID = @InquilinoId
            """,
            new { Id = contactoId, InquilinoId = inquilinoId, datos.Nombre, datos.Email, datos.Telefono, datos.RecibeAvisos, UsuarioId = usuarioId },
            cancellationToken: cancelacion));
    }

    public async Task HacerPrincipalAsync(int inquilinoId, int contactoId, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await using var transaccion = await conexion.BeginTransactionAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.CONTACTO_INQUILINO SET ES_PRINCIPAL = 0 WHERE INQUILINO_ID = @InquilinoId AND ES_PRINCIPAL = 1 AND ID <> @Id;
            UPDATE dbo.CONTACTO_INQUILINO SET ES_PRINCIPAL = 1, ACTIVO = 1, MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE ID = @Id AND INQUILINO_ID = @InquilinoId;
            """,
            new { Id = contactoId, InquilinoId = inquilinoId, UsuarioId = usuarioId }, transaccion, cancellationToken: cancelacion));
        await transaccion.CommitAsync(cancelacion);
    }

    public async Task DesactivarContactoAsync(int inquilinoId, int contactoId, int usuarioId, CancellationToken cancelacion)
    {
        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        await conexion.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.CONTACTO_INQUILINO SET ACTIVO = 0, ES_PRINCIPAL = 0, MODIFICADO_POR = @UsuarioId, FECHA_MODIFICACION = SYSUTCDATETIME()
            WHERE ID = @Id AND INQUILINO_ID = @InquilinoId
            """,
            new { Id = contactoId, InquilinoId = inquilinoId, UsuarioId = usuarioId }, cancellationToken: cancelacion));
    }

    private static object Parametros(DatosInquilino d, int usuarioId) => new
    {
        d.TipoPersona,
        d.Nombre,
        d.Dui,
        d.Nit,
        d.Nrc,
        d.RepresentanteLegal,
        d.Telefono,
        d.DireccionFiscal,
        UsuarioId = usuarioId,
    };

    private static Task<int> Bitacora(SqlConnection conexion, System.Data.Common.DbTransaction? transaccion, int usuarioId, string accion, int id, string nombre,
        CancellationToken cancelacion) =>
        conexion.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO dbo.BITACORA (USUARIO_ID, ACCION, TABLA, REGISTRO_ID, DETALLE)
            VALUES (@UsuarioId, @Accion, 'INQUILINO', @Id, (SELECT @Nombre AS nombre FOR JSON PATH, WITHOUT_ARRAY_WRAPPER))
            """,
            new { UsuarioId = usuarioId, Accion = accion, Id = id, Nombre = nombre }, transaccion, cancellationToken: cancelacion));

    private static async Task<T> ConUnicoAsync<T>(Func<Task<T>> accion)
    {
        try
        {
            return await accion();
        }
        catch (SqlException error) when (error.Number is 2627 or 2601)
        {
            throw new ReglaDeNegocioException("Ya existe un inquilino con ese DUI o NIT.", error.Number);
        }
    }
}
