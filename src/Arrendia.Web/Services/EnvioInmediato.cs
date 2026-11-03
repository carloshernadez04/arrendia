using Arrendia.Web.Data;

namespace Arrendia.Web.Services;

public sealed partial class EnvioInmediato(ICorreo correo, IRepositorioCorreos cola, ILogger<EnvioInmediato> registro)
{
    public bool PuedeEnviar => correo.PuedeEnviar;

    public async Task<bool> EnviarAsync(string tipo, int? usuarioId, MensajeCorreo mensaje, CancellationToken cancelacion)
    {
        var id = await cola.PendienteAsync(tipo, usuarioId, mensaje.Destinatario, cancelacion);
        if (!correo.PuedeEnviar)
        {
            return false;
        }

        try
        {
            await correo.EnviarAsync(mensaje, cancelacion);
            if (id is { } enviado)
            {
                await cola.MarcarAsync(enviado, true, null, cancelacion);
            }

            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Fallo(registro, tipo, error);
            if (id is { } fallido)
            {
                await cola.MarcarAsync(fallido, false, error.Message, cancelacion);
            }

            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo enviar el correo de tipo {Tipo}")]
    private static partial void Fallo(ILogger registro, string tipo, Exception error);
}
