using System.Globalization;
using System.Text.Json;
using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Inquilino)]
[Route("portal")]
public sealed partial class PortalController(
    IRepositorioPortal portal,
    IRepositorioContratos contratos,
    IRepositorioCobros cobros,
    IRepositorioDocumentos documentos,
    IRepositorioMantenimiento mantenimiento,
    IRepositorioArchivos archivos,
    IAlmacenArchivos almacen,
    IPasarelaPago pasarela,
    ProcesadorAvisos avisos,
    ServicioDocumentos servicioDocumentos,
    IHostEnvironment ambiente,
    ILogger<PortalController> registro) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancelacion)
    {
        var inquilino = User.InquilinoId();
        return View(new PortalModelo
        {
            Contratos = await portal.ContratosAsync(inquilino, cancelacion),
            Pendientes = await portal.PendientesAsync(inquilino, cancelacion),
            Pagos = (await portal.PagosAsync(inquilino, cancelacion)).Take(5).ToList(),
            Reportes = (await mantenimiento.ReportesDeInquilinoAsync(inquilino, cancelacion)).Where(r => r.Estado is "Abierto" or "Asignado" or "Terminado").ToList(),
            PagoEnLinea = pasarela.Disponible,
        });
    }

    [HttpGet("pagar")]
    public async Task<IActionResult> Pagar(int? contrato, CancellationToken cancelacion) => View(await PagarModeloAsync(contrato, null, [], null, cancelacion));

    [HttpPost("pagar")]
    public async Task<IActionResult> Pagar(int contrato, decimal? monto, List<int> cargos, CancellationToken cancelacion)
    {
        var contratosPropios = await portal.ContratosAsync(User.InquilinoId(), cancelacion);
        if (contratosPropios.All(c => c.ContratoId != contrato) || !pasarela.Disponible)
        {
            return NotFound();
        }

        var pendientes = await cobros.PendientesAsync(contrato, cancelacion);
        var elegidos = pendientes.Where(c => cargos.Contains(c.Id)).ToList();
        var error = ReglasPago.Validar(monto, elegidos.Count > 0 ? elegidos : pendientes, await cobros.PermiteParcialesAsync(contrato, cancelacion));
        if (error is not null)
        {
            return View(await PagarModeloAsync(contrato, monto, cargos, error, cancelacion));
        }

        try
        {
            var (_, referencia) = await portal.CrearIntencionAsync(contrato, monto!.Value, elegidos.Count > 0 ? cargos : null, User.Id(), cancelacion);
            var numero = contratosPropios.First(c => c.ContratoId == contrato).Numero;
            var url = await pasarela.CrearEnlaceAsync(referencia, monto.Value, $"Pago de alquiler {numero}",
                this.UrlAbsoluta($"/portal/pago/resultado?referencia={referencia}"), this.UrlAbsoluta("/wompi/aviso"), cancelacion);
            return Redirect(url);
        }
        catch (ReglaDeNegocioException ex)
        {
            return View(await PagarModeloAsync(contrato, monto, cargos, ex.Message, cancelacion));
        }
        catch (HttpRequestException ex)
        {
            PasarelaNoResponde(registro, ex);
            return View(await PagarModeloAsync(contrato, monto, cargos, "La pasarela de pago no respondió. Intenta de nuevo en unos minutos.", cancelacion));
        }
    }

    [HttpGet("pago/resultado")]
    public async Task<IActionResult> Resultado(string referencia, CancellationToken cancelacion)
    {
        var intencion = await portal.IntencionAsync(referencia, User.InquilinoId(), cancelacion);
        return intencion is null ? NotFound() : View(new ResultadoPagoModelo { Intencion = intencion });
    }

    [HttpGet("pago/simulador/{referencia}")]
    public async Task<IActionResult> Simulador(string referencia, CancellationToken cancelacion)
    {
        if (!ambiente.IsDevelopment() || pasarela is not PasarelaSimulada)
        {
            return NotFound();
        }

        var intencion = await portal.IntencionAsync(referencia, User.InquilinoId(), cancelacion);
        return intencion is null ? NotFound() : View(new SimuladorModelo { Intencion = intencion });
    }

    [HttpPost("pago/simulador/{referencia}")]
    public async Task<IActionResult> Simular(string referencia, bool aprobar, CancellationToken cancelacion)
    {
        if (!ambiente.IsDevelopment() || pasarela is not PasarelaSimulada)
        {
            return NotFound();
        }

        var intencion = await portal.IntencionAsync(referencia, User.InquilinoId(), cancelacion);
        if (intencion is null)
        {
            return NotFound();
        }

        var cuerpo = JsonSerializer.Serialize(new
        {
            IdTransaccion = $"SIM-{Guid.NewGuid():N}",
            ResultadoTransaccion = aprobar ? "ExitosaAprobada" : "Declinada",
            Monto = intencion.Monto,
            FechaTransaccion = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            FormaPagoUtilizada = "Simulador",
            EsProductiva = false,
            EnlacePago = new { IdentificadorEnlaceComercio = referencia },
        });
        await avisos.ProcesarAsync(cuerpo, FirmaWompi.Calcular(cuerpo, PasarelaSimulada.Secreto), cancelacion);
        return RedirectToAction(nameof(Resultado), new { referencia });
    }

    [HttpGet("pagos")]
    public async Task<IActionResult> Pagos(CancellationToken cancelacion) => View(await portal.PagosAsync(User.InquilinoId(), cancelacion));

    [HttpGet("pagos/{id:int}/comprobante")]
    public async Task<IActionResult> Comprobante(int id, CancellationToken cancelacion)
    {
        var pago = await cobros.PagoAsync(id, User.Id(), cancelacion);
        if (pago is null)
        {
            return NotFound();
        }

        if (pago.ComprobanteDocumentoId is { } documentoId
            && await documentos.ArchivoAsync(documentoId, cancelacion) is { } archivo
            && await almacen.AbrirAsync(archivo.PublicId, archivo.TipoMime, cancelacion) is { } flujo)
        {
            return File(flujo, "application/pdf", $"comprobante-{pago.Numero}.pdf");
        }

        var documento = await servicioDocumentos.ComprobanteAsync(id, null, cancelacion);
        return documento is null ? NotFound() : this.Pdf(documento.Pdf, $"comprobante-{pago.Numero}.pdf");
    }

    [HttpGet("documentos")]
    public async Task<IActionResult> Documentos(CancellationToken cancelacion) => View(new DocumentosPortalModelo
    {
        Documentos = await documentos.ListarAsync(User.Id(), new FiltroDocumentos(), cancelacion),
        Contratos = await portal.ContratosAsync(User.InquilinoId(), cancelacion),
    });

    [HttpGet("contratos/{id:int}/estado-cuenta")]
    public async Task<IActionResult> EstadoCuenta(int id, DateTime? desde, DateTime? hasta, CancellationToken cancelacion)
    {
        var contrato = await contratos.ObtenerAsync(id, User.Id(), cancelacion);
        if (contrato is null)
        {
            return NotFound();
        }

        var (d, h) = ContratosController.Rango(contrato, desde, hasta);
        return View("~/Views/Contratos/EstadoCuenta.cshtml", new EstadoCuentaModelo
        {
            Contrato = contrato,
            Estado = await documentos.EstadoCuentaAsync(id, d, h, User.Id(), cancelacion),
        });
    }

    [HttpPost("contratos/{id:int}/estado-cuenta/pdf")]
    public async Task<IActionResult> EstadoCuentaPdf(int id, DateTime? desde, DateTime? hasta, CancellationToken cancelacion)
    {
        var contrato = await contratos.ObtenerAsync(id, User.Id(), cancelacion);
        if (contrato is null)
        {
            return NotFound();
        }

        var (d, h) = ContratosController.Rango(contrato, desde, hasta);
        var documento = await servicioDocumentos.EstadoCuentaAsync(id, d, h, User.Id(), cancelacion);
        return this.Pdf(documento.Pdf, $"estado-de-cuenta-{contrato.Numero}.pdf");
    }

    [HttpGet("reportes")]
    public async Task<IActionResult> Reportes(CancellationToken cancelacion) =>
        View(new ReportesPortalModelo { Reportes = await mantenimiento.ReportesDeInquilinoAsync(User.InquilinoId(), cancelacion) });

    [HttpGet("reportes/nuevo")]
    public async Task<IActionResult> NuevoReporte(CancellationToken cancelacion) => View(await FormularioReporteAsync(new ReportePortalFormulario(), cancelacion));

    [HttpPost("reportes/nuevo")]
    public async Task<IActionResult> NuevoReporte(ReportePortalFormulario modelo, CancellationToken cancelacion)
    {
        await FormularioReporteAsync(modelo, cancelacion);
        var unidad = modelo.Unidades.FirstOrDefault(u => u.UnidadId == modelo.UnidadId);
        var fotos = await ReportesController.ValidarFotosAsync(modelo.Fotos, ModelState, cancelacion);
        modelo.Error = unidad is null ? "Elige tu unidad."
            : modelo.CategoriaId is null ? "Elige qué tipo de problema es."
            : string.IsNullOrWhiteSpace(modelo.Titulo) || string.IsNullOrWhiteSpace(modelo.Descripcion) ? "Escribe un título y describe el problema."
            : !Prioridades.Todas.Contains(modelo.Prioridad) ? "Elige la prioridad."
            : ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
        if (modelo.Error is not null)
        {
            return View(modelo);
        }

        try
        {
            var (id, numero, pin) = await mantenimiento.CrearReporteAsync(new NuevoReporte(unidad!.PropiedadId, unidad.UnidadId, null, modelo.CategoriaId!.Value,
                modelo.Prioridad, Recortar(modelo.Titulo!, 120), Recortar(modelo.Descripcion!, 2000)), User.Id(), cancelacion);
            await ReportesController.GuardarFotosAsync(fotos, id, User.Id(), null, "FotoReporte", archivos, almacen, cancelacion);
            this.Exito($"Recibimos tu reporte {numero}. Cuando el técnico termine, dale este PIN: {pin}. También lo ves aquí en el reporte.");
            return RedirectToAction(nameof(Reporte), new { id });
        }
        catch (ReglaDeNegocioException ex)
        {
            modelo.Error = ex.Message;
            return View(modelo);
        }
    }

    [HttpGet("reportes/{id:int}")]
    public async Task<IActionResult> Reporte(int id, CancellationToken cancelacion)
    {
        var reporte = await mantenimiento.ReporteAsync(id, User.Id(), cancelacion);
        if (reporte is null)
        {
            return NotFound();
        }

        return View(new ReporteDetalleModelo
        {
            Reporte = reporte,
            Historial = await mantenimiento.HistorialAsync(id, cancelacion),
            Fotos = await archivos.DeReporteAsync(id, cancelacion),
            Tecnicos = [],
            Permisos = Permisos.Ninguno,
            Pin = await mantenimiento.PinAsync(id, User.Id(), cancelacion),
        });
    }

    private static string Recortar(string texto, int maximo)
    {
        texto = texto.Trim();
        return texto.Length > maximo ? texto[..maximo] : texto;
    }

    private async Task<ReportePortalFormulario> FormularioReporteAsync(ReportePortalFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Unidades = await portal.UnidadesAsync(User.InquilinoId(), cancelacion);
        modelo.Categorias = await mantenimiento.CategoriasAsync(cancelacion);
        modelo.UnidadId ??= modelo.Unidades.Count == 1 ? modelo.Unidades[0].UnidadId : null;
        return modelo;
    }

    private async Task<PagarModelo> PagarModeloAsync(int? contrato, decimal? monto, List<int> cargos, string? error, CancellationToken cancelacion)
    {
        var inquilino = User.InquilinoId();
        var lista = await portal.ContratosAsync(inquilino, cancelacion);
        var elegido = contrato ?? (lista.Count == 1 ? lista[0].ContratoId : null);
        return new PagarModelo
        {
            Contratos = lista,
            ContratoId = elegido,
            Pendientes = (await portal.PendientesAsync(inquilino, cancelacion)).Where(c => c.ContratoId == elegido).ToList(),
            PermiteParciales = elegido is { } id && await cobros.PermiteParcialesAsync(id, cancelacion),
            Monto = monto,
            Cargos = cargos,
            Error = error,
            Disponible = pasarela.Disponible,
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "La pasarela de pago no respondió")]
    private static partial void PasarelaNoResponde(ILogger registro, Exception error);
}

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("wompi")]
public sealed class WompiController(ProcesadorAvisos avisos) : Controller
{
    [HttpPost("aviso")]
    public async Task<IActionResult> Aviso(CancellationToken cancelacion)
    {
        using var lector = new StreamReader(Request.Body);
        var cuerpo = await lector.ReadToEndAsync(cancelacion);
        if (cuerpo.Length is 0 or > 100_000)
        {
            return BadRequest();
        }

        var firma = Request.Headers["wompi_hash"].FirstOrDefault() ?? Request.Headers["Wompi-Hash"].FirstOrDefault();
        try
        {
            var resultado = await avisos.ProcesarAsync(cuerpo, firma, cancelacion);
            return resultado == "rechazado" ? Unauthorized() : Ok();
        }
        catch (ReglaDeNegocioException)
        {
            return Ok();
        }
    }
}
