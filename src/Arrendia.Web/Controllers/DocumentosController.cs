using System.Security.Cryptography;
using Arrendia.Web.Data;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arrendia.Web.Controllers;

[Route("archivos")]
public sealed class ArchivosController(IRepositorioArchivos archivos, IAlmacenArchivos almacen) : Controller
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Ver(int id, bool descargar, CancellationToken cancelacion)
    {
        var archivo = await archivos.ObtenerSiPuedeVerAsync(id, User.Id(), cancelacion);
        if (archivo is null)
        {
            return NotFound();
        }

        var flujo = await almacen.AbrirAsync(archivo.PublicId, archivo.TipoMime, cancelacion);
        if (flujo is null)
        {
            return NotFound();
        }

        return descargar ? File(flujo, archivo.TipoMime, archivo.NombreOriginal) : File(flujo, archivo.TipoMime);
    }
}

[Route("documentos")]
public sealed class DocumentosController(IRepositorioDocumentos documentos, IAlmacenArchivos almacen) : Controller
{
    public static readonly string[] Tipos = ["Contrato", "Comprobante", "AvisoCobro", "EstadoCuenta"];

    [Authorize(Roles = Roles.Personal)]
    [HttpGet("")]
    public async Task<IActionResult> Index(string? tipo, string? buscar, CancellationToken cancelacion)
    {
        var filtro = new FiltroDocumentos(Tipos.Contains(tipo) ? tipo : null, buscar);
        return View(new DocumentosModelo { Filtro = filtro, Documentos = await documentos.ListarAsync(User.Id(), filtro, cancelacion) });
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken cancelacion)
    {
        var documento = await documentos.ObtenerAsync(id, User.Id(), cancelacion);
        if (documento is null)
        {
            return NotFound();
        }

        var archivo = await documentos.ArchivoAsync(documento.Id, cancelacion);
        var flujo = archivo is null ? null : await almacen.AbrirAsync(archivo.PublicId, archivo.TipoMime, cancelacion);
        return flujo is null ? NotFound() : File(flujo, "application/pdf", $"{documento.Folio}.pdf");
    }
}

[AllowAnonymous]
[Route("verificar")]
public sealed class VerificarController(IRepositorioDocumentos documentos) : Controller
{
    [HttpGet("")]
    public IActionResult Index(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo)
            ? View(new VerificarModelo())
            : RedirectToAction(nameof(Codigo), new { codigo = codigo.Trim().ToUpperInvariant() });

    [HttpGet("{codigo}")]
    [EnableRateLimiting(CuentaController.PoliticaLimite)]
    public async Task<IActionResult> Codigo(string codigo, CancellationToken cancelacion) =>
        View("Index", await ModeloAsync(codigo, cancelacion));

    [HttpPost("{codigo}")]
    [EnableRateLimiting(CuentaController.PoliticaLimite)]
    public async Task<IActionResult> Comparar(string codigo, IFormFile? archivo, CancellationToken cancelacion)
    {
        var modelo = await ModeloAsync(codigo, cancelacion);
        if (modelo.Documento is null)
        {
            return View("Index", modelo);
        }

        if (archivo is null || archivo.Length == 0 || archivo.Length > TiposArchivo.TamanoMaximo)
        {
            modelo.Comparacion = "Elige el PDF que quieres comparar (máximo 10 MB).";
            return View("Index", modelo);
        }

        await using var flujo = archivo.OpenReadStream();
        var hash = await SHA256.HashDataAsync(flujo, cancelacion);
        modelo.Coincide = CryptographicOperations.FixedTimeEquals(hash, modelo.Documento.HashSha256);
        modelo.Comparacion = modelo.Coincide == true
            ? "El archivo es idéntico al documento original."
            : "El archivo no coincide con el original: pudo haber sido modificado.";
        return View("Index", modelo);
    }

    private async Task<VerificarModelo> ModeloAsync(string codigo, CancellationToken cancelacion)
    {
        codigo = codigo.Trim().ToUpperInvariant();
        var valido = codigo.Length == 12 && codigo.All(char.IsAsciiLetterOrDigit);
        return new VerificarModelo
        {
            Codigo = codigo,
            Buscado = true,
            Documento = valido ? await documentos.VerificarAsync(codigo, cancelacion) : null,
        };
    }
}
