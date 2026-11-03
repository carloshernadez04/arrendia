using Arrendia.Web.Data;

namespace Arrendia.Web.Services;

public sealed record ResultadoInvitacion(int UsuarioId, string? EnlaceParaCopiar);

public sealed class ServicioInvitaciones(IRepositorioUsuarios usuarios, EnvioInmediato envio, ServicioDocumentos documentos)
{
    private const int DiasVigencia = 7;

    public async Task<ResultadoInvitacion> InvitarAsync(string nombre, string email, string rol, int? inquilinoId, IReadOnlyCollection<int>? propiedades,
        int usuarioId, CancellationToken cancelacion)
    {
        var token = TokenSeguro.Generar();
        var id = await usuarios.InvitarAsync(nombre, email, rol, inquilinoId, propiedades, TokenSeguro.Hash(token), envio.PuedeEnviar, usuarioId, cancelacion);
        return new ResultadoInvitacion(id, await EnviarAsync(id, nombre, email, rol, token, cancelacion));
    }

    public async Task<string?> ReenviarAsync(int invitadoId, int usuarioId, CancellationToken cancelacion)
    {
        var invitado = await usuarios.ObtenerAsync(invitadoId, cancelacion) ?? throw new ReglaDeNegocioException("El usuario no existe.", 0);
        var token = TokenSeguro.Generar();
        await usuarios.ReenviarAsync(invitadoId, TokenSeguro.Hash(token), envio.PuedeEnviar, usuarioId, cancelacion);
        return await EnviarAsync(invitadoId, invitado.Nombre, invitado.Email, invitado.Rol, token, cancelacion);
    }

    private async Task<string?> EnviarAsync(int id, string nombre, string email, string rol, string token, CancellationToken cancelacion)
    {
        var enlace = $"{documentos.UrlPublica()}/cuenta/activar/{token}";
        var enviado = await envio.EnviarAsync("Invitacion", id, PlantillasCorreo.Invitacion(email.Trim().ToLowerInvariant(), nombre, enlace, DiasVigencia, rol), cancelacion);
        return enviado ? null : enlace;
    }
}
