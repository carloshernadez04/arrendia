using System.Threading.Channels;
using Arrendia.Web.Data;

namespace Arrendia.Web.Services;

public sealed record EventoNotificacion(string Tipo, int? Id = null, string? Numero = null);

public sealed class ColaNotificaciones
{
    private readonly Channel<EventoNotificacion> _canal = Channel.CreateBounded<EventoNotificacion>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public void Agregar(EventoNotificacion evento) => _canal.Writer.TryWrite(evento);

    public IAsyncEnumerable<EventoNotificacion> LeerAsync(CancellationToken cancelacion) => _canal.Reader.ReadAllAsync(cancelacion);
}

public sealed partial class EnvioNotificaciones(ColaNotificaciones cola, IServiceScopeFactory alcances, ILogger<EnvioNotificaciones> registro) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evento in cola.LeerAsync(stoppingToken))
        {
            try
            {
                await using var alcance = alcances.CreateAsyncScope();
                await EnviarAsync(alcance.ServiceProvider, evento, stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Fallo(registro, evento.Tipo, error);
            }
        }
    }

    private async Task EnviarAsync(IServiceProvider servicios, EventoNotificacion evento, CancellationToken cancelacion)
    {
        var correo = servicios.GetRequiredService<ICorreo>();
        if (!correo.PuedeEnviar)
        {
            return;
        }

        var notificaciones = servicios.GetRequiredService<IRepositorioNotificaciones>();
        var notificacion = await notificaciones.BuscarAsync(evento.Tipo, evento.Id, evento.Numero, cancelacion);
        if (notificacion is null)
        {
            return;
        }

        var enlace = servicios.GetRequiredService<ServicioDocumentos>().UrlPublica() + notificacion.Ruta;
        foreach (var destinatario in await notificaciones.DestinatariosAsync(notificacion.PropiedadId, cancelacion))
        {
            try
            {
                await correo.EnviarAsync(PlantillasCorreo.Notificacion(destinatario.Email, destinatario.Nombre, notificacion, enlace), cancelacion);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Fallo(registro, evento.Tipo, error);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo enviar la notificación de tipo {Tipo}")]
    private static partial void Fallo(ILogger registro, string tipo, Exception error);
}
