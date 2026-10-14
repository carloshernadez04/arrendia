using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Arrendia.Web.Services;

public sealed class OpcionesSmtp
{
    public const string Seccion = "Smtp";

    public string? Host { get; set; }
    public int Puerto { get; set; } = 587;
    public string? Usuario { get; set; }
    public string? Contrasena { get; set; }
    public string Remitente { get; set; } = "cobros@arrendia.site";
    public string NombreRemitente { get; set; } = "Arrendia";

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(Host);
}

public sealed record MensajeCorreo(string Destinatario, string Asunto, string Html, string Texto);

public interface ICorreo
{
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion);
}

public sealed class CorreoSmtp(Microsoft.Extensions.Options.IOptions<OpcionesSmtp> opciones) : ICorreo
{
    private readonly OpcionesSmtp _opciones = opciones.Value;

    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion)
    {
        using var correo = new MimeMessage();
        correo.From.Add(new MailboxAddress(_opciones.NombreRemitente, _opciones.Remitente));
        correo.To.Add(MailboxAddress.Parse(mensaje.Destinatario));
        correo.Subject = mensaje.Asunto;
        correo.Body = new BodyBuilder { HtmlBody = mensaje.Html, TextBody = mensaje.Texto }.ToMessageBody();

        using var cliente = new SmtpClient { Timeout = 15000 };
        await cliente.ConnectAsync(_opciones.Host!, _opciones.Puerto, SecureSocketOptions.StartTlsWhenAvailable, cancelacion);
        if (!string.IsNullOrEmpty(_opciones.Usuario))
        {
            await cliente.AuthenticateAsync(_opciones.Usuario, _opciones.Contrasena ?? "", cancelacion);
        }

        await cliente.SendAsync(correo, cancelacion);
        await cliente.DisconnectAsync(true, cancelacion);
    }
}

public sealed partial class CorreoEnRegistro(ILogger<CorreoEnRegistro> registro, IHostEnvironment ambiente) : ICorreo
{
    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion)
    {
        if (ambiente.IsDevelopment())
        {
            CorreoDeDesarrollo(registro, mensaje.Destinatario, mensaje.Asunto, mensaje.Texto);
            return Task.CompletedTask;
        }

        throw new InvalidOperationException("No hay un servidor SMTP configurado.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Correo de desarrollo para {Destinatario} · {Asunto}\n{Texto}")]
    private static partial void CorreoDeDesarrollo(ILogger registro, string destinatario, string asunto, string texto);
}

public static class PlantillasCorreo
{
    public static MensajeCorreo Recuperacion(string destinatario, string nombre, string enlace, int minutos)
    {
        var texto = $"Hola, {nombre}:\n\nPediste crear una contraseña nueva para Arrendia. Abre este enlace (vence en {minutos} minutos):\n{enlace}\n\nSi no fuiste tú, ignora este correo: tu contraseña no cambia.";
        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;font-size:15px;color:#1f1f1e;max-width:520px">
            <p>Hola, {System.Net.WebUtility.HtmlEncode(nombre)}:</p>
            <p>Pediste crear una contraseña nueva para Arrendia. El enlace vence en {minutos} minutos.</p>
            <p><a href="{enlace}" style="display:inline-block;background:#2f5bd3;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none">Crear contraseña nueva</a></p>
            <p style="color:#5e5e5a;font-size:13px">Si no fuiste tú, ignora este correo: tu contraseña no cambia.</p>
            </div>
            """;
        return new MensajeCorreo(destinatario, "Crea una contraseña nueva", html, texto);
    }
}
