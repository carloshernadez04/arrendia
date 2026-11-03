using Dapper;

namespace Arrendia.Web.Data;

public sealed class Permisos
{
    public bool EsAdministrador { get; set; }
    public bool CrearContratos { get; set; }
    public bool ActivarContratos { get; set; }
    public bool CambiarCondiciones { get; set; }
    public bool RegistrarPagos { get; set; }
    public bool CargosManuales { get; set; }
    public bool Condonar { get; set; }
    public bool GestionarReportes { get; set; }

    public static readonly Permisos Ninguno = new();
}

public interface IRepositorioPermisos
{
    Task<Permisos> DeAsync(int usuarioId, CancellationToken cancelacion);
}

public sealed class RepositorioPermisos(IBaseDatos baseDatos) : IRepositorioPermisos
{
    private Permisos? _cache;
    private int _cacheUsuario;

    public async Task<Permisos> DeAsync(int usuarioId, CancellationToken cancelacion)
    {
        if (_cache is not null && _cacheUsuario == usuarioId)
        {
            return _cache;
        }

        await using var conexion = await baseDatos.AbrirAsync(cancelacion);
        _cache = await conexion.QuerySingleAsync<Permisos>(new CommandDefinition(
            """
            SELECT CAST(dbo.FN_TIENE_PERMISO(@Id, 'ADMIN') AS BIT) AS ES_ADMINISTRADOR,
                   CAST(dbo.FN_TIENE_PERMISO(@Id, 'CREAR_CONTRATOS') AS BIT) AS CREAR_CONTRATOS,
                   CAST(dbo.FN_TIENE_PERMISO(@Id, 'ACTIVAR_CONTRATOS') AS BIT) AS ACTIVAR_CONTRATOS,
                   CAST(dbo.FN_TIENE_PERMISO(@Id, 'CAMBIAR_CONDICIONES') AS BIT) AS CAMBIAR_CONDICIONES,
                   CAST(dbo.FN_TIENE_PERMISO(@Id, 'REGISTRAR_PAGOS') AS BIT) AS REGISTRAR_PAGOS,
                   CAST(dbo.FN_TIENE_PERMISO(@Id, 'CARGOS_MANUALES') AS BIT) AS CARGOS_MANUALES,
                   CAST(dbo.FN_TIENE_PERMISO(@Id, 'CONDONAR') AS BIT) AS CONDONAR,
                   CAST(dbo.FN_TIENE_PERMISO(@Id, 'GESTIONAR_REPORTES') AS BIT) AS GESTIONAR_REPORTES
            """,
            new { Id = usuarioId }, cancellationToken: cancelacion));
        _cacheUsuario = usuarioId;
        return _cache;
    }
}
