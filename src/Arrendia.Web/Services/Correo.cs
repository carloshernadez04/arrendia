using System.Net;
using Arrendia.Web.Models;
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

public sealed record Adjunto(string Nombre, byte[] Contenido, string TipoMime);

public sealed record MensajeCorreo(string Destinatario, string Asunto, string Html, string Texto, Adjunto? Adjunto = null);

public interface ICorreo
{
    bool PuedeEnviar { get; }
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion);
}

public sealed class CorreoSmtp(Microsoft.Extensions.Options.IOptions<OpcionesSmtp> opciones) : ICorreo
{
    private readonly OpcionesSmtp _opciones = opciones.Value;

    public bool PuedeEnviar => true;

    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion)
    {
        using var correo = new MimeMessage();
        correo.From.Add(new MailboxAddress(_opciones.NombreRemitente, _opciones.Remitente));
        correo.To.Add(MailboxAddress.Parse(mensaje.Destinatario));
        correo.Subject = mensaje.Asunto;
        var cuerpo = new BodyBuilder { HtmlBody = mensaje.Html, TextBody = mensaje.Texto };
        if (mensaje.Adjunto is { } adjunto)
        {
            cuerpo.Attachments.Add(adjunto.Nombre, adjunto.Contenido, ContentType.Parse(adjunto.TipoMime));
        }

        correo.Body = cuerpo.ToMessageBody();

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
    public bool PuedeEnviar => ambiente.IsDevelopment();

    public Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion)
    {
        if (ambiente.IsDevelopment())
        {
            CorreoDeDesarrollo(registro, mensaje.Destinatario, mensaje.Asunto, mensaje.Texto, mensaje.Adjunto?.Nombre ?? "sin adjunto");
            return Task.CompletedTask;
        }

        throw new InvalidOperationException("No hay un servidor SMTP configurado.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Correo de desarrollo para {Destinatario} · {Asunto} ({Adjunto})\n{Texto}")]
    private static partial void CorreoDeDesarrollo(ILogger registro, string destinatario, string asunto, string texto, string adjunto);
}

public static class PlantillasCorreo
{
    public static MensajeCorreo Recuperacion(string destinatario, string nombre, string enlace, int minutos) =>
        Armar(destinatario, "Crea una contraseña nueva",
            $"Hola, {nombre}:",
            [$"Pediste crear una contraseña nueva para Arrendia. El enlace vence en {minutos} minutos."],
            ("Crear contraseña nueva", enlace),
            "Si no fuiste tú, ignora este correo: tu contraseña no cambia.");

    public static MensajeCorreo Invitacion(string destinatario, string nombre, string enlace, int dias, string rol) =>
        Armar(destinatario, "Te invitaron a Arrendia",
            $"Hola, {nombre}:",
            [rol == Roles.Inquilino
                ? "Ya tienes acceso al portal de inquilinos de Arrendia. Desde ahí puedes ver tu estado de cuenta, pagar con tarjeta, descargar tus comprobantes y reportar problemas de tu unidad."
                : "Te dieron acceso a Arrendia para administrar propiedades.",
             $"Abre el enlace para elegir tu contraseña. Vence en {dias} días y se usa una sola vez."],
            ("Activar mi cuenta", enlace),
            "Si no esperabas este correo, ignóralo.");

    public static MensajeCorreo ConfirmarCorreo(string destinatario, string nombre, string enlace) =>
        Armar(destinatario, "Confirma tu nuevo correo de acceso",
            $"Hola, {nombre}:",
            ["Pediste usar este correo para entrar a Arrendia. Confírmalo con el enlace (vence en 24 horas)."],
            ("Confirmar correo", enlace),
            "Si no lo pediste, ignora este correo: tu cuenta no cambia.");

    public static MensajeCorreo CorreoCambiado(string destinatario, string nombre, string nuevo) =>
        Armar(destinatario, "Tu correo de acceso a Arrendia cambió",
            $"Hola, {nombre}:",
            [$"Desde ahora entras a Arrendia con {nuevo}.",
             "Si no fuiste tú, comunícate con la administración de inmediato."],
            null, null);

    public static MensajeCorreo EnlaceTecnico(string destinatario, string tecnico, string numero, string titulo, string direccion, string enlace, int horas) =>
        Armar(destinatario, $"Trabajo asignado {numero}",
            $"Hola, {tecnico}:",
            [$"Te asignaron el trabajo {numero}: {titulo}.",
             $"Lugar: {direccion}.",
             $"En el enlace ves los detalles y las fotos. Al terminar, pide el PIN a quien reportó el problema, sube las fotos del trabajo y ciérralo. El enlace vence en {horas} horas."],
            ("Ver el trabajo", enlace),
            null);

    public static MensajeCorreo AvisoCobro(string destinatario, string asunto, string inquilino, string descripcion, decimal monto, DateTime vence, Adjunto? pdf) =>
        Armar(destinatario, asunto,
            $"Estimado(a) {inquilino}:",
            [$"Te compartimos el aviso de cobro de {descripcion} por {Formato.Dinero(monto)}, con vencimiento el {Formato.FechaLarga(vence)}.",
             "Puedes pagar con tarjeta desde el portal del inquilino, o en efectivo o transferencia con la administración."],
            null,
            "El aviso va adjunto en PDF.") with
        { Adjunto = pdf };

    public static MensajeCorreo Comprobante(string destinatario, string asunto, string inquilino, string numero, decimal monto, string metodo, Adjunto? pdf) =>
        Armar(destinatario, asunto,
            $"Estimado(a) {inquilino}:",
            [$"Recibimos tu pago {numero} por {Formato.Dinero(monto)} ({metodo.ToLowerInvariant()}). Gracias."],
            null,
            "El comprobante va adjunto en PDF.") with
        { Adjunto = pdf };

    public static MensajeCorreo Notificacion(string destinatario, string nombre, Data.Notificacion notificacion, string enlace) =>
        Armar(destinatario, $"{notificacion.Titulo}: {notificacion.Detalle}",
            $"Hola, {nombre}:",
            [notificacion.Tipo switch
            {
                "Reporte" => $"Un inquilino reportó un problema: {notificacion.Detalle} ({notificacion.Numero}). Revísalo y asígnale un técnico.",
                "Terminado" => $"El técnico marcó como terminado el trabajo {notificacion.Numero}: {notificacion.Detalle}. Revisa las fotos y ciérralo.",
                _ => $"{notificacion.Detalle} pagó {Formato.Dinero(notificacion.Monto)} con tarjeta desde el portal. El pago {notificacion.Numero} ya quedó aplicado.",
            }],
            (notificacion.Tipo == "Pago" ? "Ver el pago" : "Ver el reporte", enlace),
            "Recibes este aviso porque administras esta propiedad en Arrendia.");

    private static MensajeCorreo Armar(string destinatario, string asunto, string saludo, string[] parrafos, (string Texto, string Url)? boton, string? nota)
    {
        var texto = $"{saludo}\n\n{string.Join("\n\n", parrafos)}" +
            (boton is { } b ? $"\n\n{b.Texto}: {b.Url}" : "") +
            (nota is null ? "" : $"\n\n{nota}");
        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;font-size:15px;line-height:1.5;color:#1f1f1e;max-width:540px">
            <p>{WebUtility.HtmlEncode(saludo)}</p>
            {string.Concat(parrafos.Select(p => $"<p>{WebUtility.HtmlEncode(p)}</p>"))}
            {(boton is { } c ? $"<p><a href=\"{WebUtility.HtmlEncode(c.Url)}\" style=\"display:inline-block;background:#2f5bd3;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none\">{WebUtility.HtmlEncode(c.Texto)}</a></p>" : "")}
            {(nota is null ? "" : $"<p style=\"color:#5e5e5a;font-size:13px\">{WebUtility.HtmlEncode(nota)}</p>")}
            </div>
            """;
        return new MensajeCorreo(destinatario, asunto, html, texto);
    }
}
