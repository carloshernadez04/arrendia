using Arrendia.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Infraestructura;

public static class ExtensionesControlador
{
    public static void Exito(this Controller controlador, string mensaje) => controlador.TempData["Mensaje"] = mensaje;

    public static void Fallo(this Controller controlador, string mensaje) => controlador.TempData["Error"] = mensaje;

    public static void EnlaceParaCopiar(this Controller controlador, string titulo, string url)
    {
        controlador.TempData["EnlaceTitulo"] = titulo;
        controlador.TempData["Enlace"] = url;
    }

    public static string UrlAbsoluta(this Controller controlador, string ruta) =>
        controlador.HttpContext.RequestServices.GetRequiredService<ServicioDocumentos>().UrlPublica() + ruta;

    public static FileContentResult Pdf(this Controller controlador, byte[] contenido, string nombre) =>
        controlador.File(contenido, "application/pdf", nombre);
}
