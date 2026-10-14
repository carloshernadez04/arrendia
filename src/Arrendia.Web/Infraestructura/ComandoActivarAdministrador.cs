using Arrendia.Web.Data;
using Arrendia.Web.Services;

namespace Arrendia.Web.Infraestructura;

public static class ComandoActivarAdministrador
{
    public const string Nombre = "activar-admin";

    public static async Task<int> EjecutarAsync(IServiceProvider servicios, string? email)
    {
        using var alcance = servicios.CreateScope();
        var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorioCuentas>();
        var configuracion = alcance.ServiceProvider.GetRequiredService<IConfiguration>();

        var usuarioId = await repositorio.AdministradorPendienteAsync(
            string.IsNullOrWhiteSpace(email) ? null : ReglasContrasena.NormalizarEmail(email), CancellationToken.None);
        if (usuarioId is null)
        {
            await Console.Error.WriteLineAsync("No hay un administrador pendiente de activar con ese correo.");
            return 1;
        }

        var token = TokenSeguro.Generar();
        await repositorio.GenerarInvitacionAsync(usuarioId.Value, TokenSeguro.Hash(token), CancellationToken.None);

        var baseUrl = (configuracion["App:UrlPublica"] ?? "").TrimEnd('/');
        await Console.Out.WriteLineAsync($"Enlace de activación (vence en 7 días, un solo uso):{Environment.NewLine}{baseUrl}/cuenta/activar/{token}");
        return 0;
    }
}
