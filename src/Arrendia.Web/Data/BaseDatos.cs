using System.Data;
using System.Security.Claims;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Arrendia.Web.Data;

public interface IBaseDatos
{
    Task<SqlConnection> AbrirAsync(CancellationToken cancelacion = default);
}

public sealed class BaseDatos(IConfiguration configuracion, IHttpContextAccessor contexto) : IBaseDatos
{
    public const string NombreCadena = "Arrendia";

    public async Task<SqlConnection> AbrirAsync(CancellationToken cancelacion = default)
    {
        var cadena = configuracion.GetConnectionString(NombreCadena)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión «{NombreCadena}».");
        var conexion = new SqlConnection(cadena);
        try
        {
            await conexion.OpenAsync(cancelacion);
            var usuarioId = UsuarioActual();
            if (usuarioId is not null)
            {
                await conexion.ExecuteAsync(new CommandDefinition(
                    "EXEC sp_set_session_context @key = N'USUARIO_ID', @value = @UsuarioId;",
                    new { UsuarioId = usuarioId },
                    cancellationToken: cancelacion));
            }

            return conexion;
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
    }

    private int? UsuarioActual()
    {
        var valor = contexto.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(valor, out var id) ? id : null;
    }

    public static void Configurar()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}

public sealed class ReglaDeNegocioException(string message, int numero) : Exception(message)
{
    public int Numero { get; } = numero;

    public static bool EsReglaDeNegocio(SqlException error) => error.Number is >= 50000 and < 60000;
}

public static class EjecucionSegura
{
    public static async Task<T> ConReglasAsync<T>(Func<Task<T>> accion)
    {
        try
        {
            return await accion();
        }
        catch (SqlException error) when (ReglaDeNegocioException.EsReglaDeNegocio(error))
        {
            throw new ReglaDeNegocioException(error.Message, error.Number);
        }
    }

    public static async Task ConReglasAsync(Func<Task> accion)
    {
        try
        {
            await accion();
        }
        catch (SqlException error) when (ReglaDeNegocioException.EsReglaDeNegocio(error))
        {
            throw new ReglaDeNegocioException(error.Message, error.Number);
        }
    }

    public static CommandDefinition Procedimiento(string nombre, object? parametros, CancellationToken cancelacion) =>
        new(nombre, parametros, commandType: CommandType.StoredProcedure, cancellationToken: cancelacion);
}
