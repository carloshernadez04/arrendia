using Arrendia.Web.Data;
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Arrendia.Web.Infraestructura;

public sealed class SaludBaseDatos(IBaseDatos baseDatos) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conexion = await baseDatos.AbrirAsync(cancellationToken);
            await conexion.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
            return HealthCheckResult.Healthy();
        }
        catch (Exception error) when (error is Microsoft.Data.SqlClient.SqlException or InvalidOperationException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("No se pudo conectar con la base de datos.");
        }
    }
}
