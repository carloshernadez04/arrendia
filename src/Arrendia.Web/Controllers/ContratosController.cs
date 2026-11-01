using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Personal)]
[Route("contratos")]
public sealed class ContratosController(
    IRepositorioContratos repositorio,
    IRepositorioPropiedades propiedades,
    IRepositorioInquilinos inquilinos,
    IRepositorioConfiguracion configuracion,
    IRepositorioDocumentos documentos,
    IRepositorioArchivos archivos,
    IRepositorioPermisos permisos,
    IAlmacenArchivos almacen,
    ServicioDocumentos servicioDocumentos) : Controller
{
    public static readonly string[] EstadosContrato = ["Borrador", "PendienteFirma", "Activo", "Finalizado", "Anulado"];

    [HttpGet("")]
    public async Task<IActionResult> Index(string? estado, int? propiedad, string? buscar, CancellationToken cancelacion)
    {
        var filtro = new FiltroContratos(EstadosContrato.Contains(estado) ? estado : null, propiedad, buscar);
        return View(new ContratosModelo
        {
            Contratos = await repositorio.ListarAsync(User.Id(), filtro, cancelacion),
            Propiedades = await propiedades.ListarAsync(User.Id(), cancelacion),
            Filtro = filtro,
            Permisos = await permisos.DeAsync(User.Id(), cancelacion),
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancelacion)
    {
        var contrato = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (contrato is null)
        {
            return NotFound();
        }

        return View(new ContratoDetalleModelo
        {
            Contrato = contrato,
            Condiciones = await repositorio.CondicionesAsync(id, cancelacion),
            Cargos = await repositorio.CargosAsync(id, cancelacion),
            Pagos = await repositorio.PagosAsync(id, cancelacion),
            Documentos = await documentos.ListarAsync(User.Id(), new FiltroDocumentos(ContratoId: id), cancelacion),
            Permisos = await permisos.DeAsync(User.Id(), cancelacion),
        });
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Nuevo(int? unidad, int? inquilino, CancellationToken cancelacion)
    {
        if (!(await permisos.DeAsync(User.Id(), cancelacion)).CrearContratos)
        {
            return Forbid();
        }

        var hoy = Formato.HoySv();
        var inicio = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1);
        var modelo = new ContratoFormulario { UnidadId = unidad, InquilinoId = inquilino, FechaInicio = inicio, FechaFin = inicio.AddYears(1).AddDays(-1) };
        await CargarOpcionesAsync(modelo, cancelacion);
        modelo.PlantillaId = modelo.Plantillas.FirstOrDefault(p => p.EsPredeterminada)?.Id;
        modelo.Renta = modelo.Unidades.FirstOrDefault(u => u.Id == unidad)?.RentaReferencia;
        return View(modelo);
    }

    [HttpPost("nuevo")]
    public async Task<IActionResult> Nuevo(ContratoFormulario modelo, CancellationToken cancelacion)
    {
        if (!(await permisos.DeAsync(User.Id(), cancelacion)).CrearContratos)
        {
            return Forbid();
        }

        if (modelo.FechaInicio is { } inicio && modelo.FechaFin is { } fin && fin <= inicio)
        {
            ModelState.AddModelError(nameof(modelo.FechaFin), "La fecha de fin debe ser posterior a la de inicio.");
        }

        if (!ModelState.IsValid)
        {
            await CargarOpcionesAsync(modelo, cancelacion);
            return View(modelo);
        }

        try
        {
            var (id, numero) = await repositorio.CrearAsync(new NuevoContrato(modelo.UnidadId!.Value, modelo.InquilinoId!.Value, modelo.FechaInicio!.Value,
                modelo.FechaFin!.Value, modelo.Renta!.Value, modelo.MontoDeposito, modelo.PlantillaId, modelo.DiaPago, modelo.DiasGracia,
                Opciones.SiNo(modelo.AplicaMora), modelo.PorcentajeMora, Opciones.SiNo(modelo.PermiteParciales)), User.Id(), cancelacion);
            this.Exito($"Contrato {numero} creado en borrador. Genera el PDF para firmarlo.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError("", error.Message);
            await CargarOpcionesAsync(modelo, cancelacion);
            return View(modelo);
        }
    }

    [HttpPost("{id:int}/generar")]
    public async Task<IActionResult> Generar(int id, CancellationToken cancelacion)
    {
        var contrato = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (contrato is null)
        {
            return NotFound();
        }

        try
        {
            var documento = await servicioDocumentos.ContratoAsync(id, User.Id(), cancelacion);
            this.Exito($"Contrato generado con el folio {documento?.Folio}. Descárgalo, imprímelo y fírmenlo.");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/activar")]
    public async Task<IActionResult> Activar(int id, IFormFile? firmado, CancellationToken cancelacion)
    {
        var contrato = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (contrato is null)
        {
            return NotFound();
        }

        if (!(await permisos.DeAsync(User.Id(), cancelacion)).ActivarContratos)
        {
            return Forbid();
        }

        var (tipo, error) = await TiposArchivo.ValidarAsync(firmado, TiposArchivo.ImagenesYPdf, cancelacion);
        if (error is not null)
        {
            this.Fallo(error);
            return RedirectToAction(nameof(Detalle), new { id });
        }

        try
        {
            string publicId;
            await using (var flujo = firmado!.OpenReadStream())
            {
                publicId = await almacen.GuardarAsync(flujo, tipo!, "contratos", cancelacion);
            }

            var archivoId = await archivos.RegistrarAsync(new NuevoArchivo(publicId, $"contrato-firmado-{contrato.Numero}{TiposArchivo.Extension(tipo!)}",
                tipo!, firmado.Length, "ContratoFirmado", ContratoId: id, UsuarioId: User.Id()), cancelacion);
            await repositorio.ActivarAsync(id, archivoId, User.Id(), cancelacion);
            this.Exito($"Contrato {contrato.Numero} activado. La unidad quedó ocupada y los cobros de renta se generan solos.");
        }
        catch (ReglaDeNegocioException ex)
        {
            this.Fallo(ex.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/condiciones")]
    public async Task<IActionResult> Condiciones(int id, CondicionesFormulario modelo, CancellationToken cancelacion)
    {
        if (await repositorio.ObtenerAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            this.Fallo(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return RedirectToAction(nameof(Detalle), new { id });
        }

        try
        {
            await repositorio.CambiarCondicionesAsync(id, new CambioCondiciones(modelo.VigenteDesde!.Value, modelo.Renta, modelo.DiaPago, modelo.DiasGracia,
                Opciones.SiNo(modelo.AplicaMora), modelo.PorcentajeMora, Opciones.SiNo(modelo.PermiteParciales), modelo.Motivo!.Trim()), User.Id(), cancelacion);
            this.Exito($"Nuevas condiciones a partir del {Formato.Fecha(modelo.VigenteDesde)}. Los cargos ya emitidos no cambian.");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/finalizar")]
    public async Task<IActionResult> Finalizar(int id, DateTime? fecha, string? motivo, CancellationToken cancelacion)
    {
        if (await repositorio.ObtenerAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        try
        {
            await repositorio.FinalizarAsync(id, fecha ?? Formato.HoySv(), motivo?.Trim() ?? "", User.Id(), cancelacion);
            this.Exito("Contrato finalizado. La unidad quedó disponible; los saldos pendientes se siguen pudiendo cobrar.");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id, string? motivo, CancellationToken cancelacion)
    {
        if (await repositorio.ObtenerAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        try
        {
            await repositorio.AnularAsync(id, motivo?.Trim() ?? "", User.Id(), cancelacion);
            this.Exito("Contrato anulado.");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpGet("{id:int}/estado-cuenta")]
    public async Task<IActionResult> EstadoCuenta(int id, DateTime? desde, DateTime? hasta, CancellationToken cancelacion)
    {
        var contrato = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (contrato is null)
        {
            return NotFound();
        }

        var (d, h) = Rango(contrato, desde, hasta);
        return View(new EstadoCuentaModelo
        {
            Contrato = contrato,
            Estado = await documentos.EstadoCuentaAsync(id, d, h, User.Id(), cancelacion),
        });
    }

    [HttpPost("{id:int}/estado-cuenta/pdf")]
    public async Task<IActionResult> EstadoCuentaPdf(int id, DateTime? desde, DateTime? hasta, CancellationToken cancelacion)
    {
        var contrato = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (contrato is null)
        {
            return NotFound();
        }

        var (d, h) = Rango(contrato, desde, hasta);
        var documento = await servicioDocumentos.EstadoCuentaAsync(id, d, h, User.Id(), cancelacion);
        return this.Pdf(documento.Pdf, $"estado-de-cuenta-{contrato.Numero}.pdf");
    }

    public static (DateTime Desde, DateTime Hasta) Rango(ContratoDetalle contrato, DateTime? desde, DateTime? hasta)
    {
        var h = (hasta ?? Formato.HoySv()).Date;
        var d = (desde ?? contrato.FechaInicio).Date;
        return d > h ? (h, d) : (d, h);
    }

    private async Task CargarOpcionesAsync(ContratoFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Unidades = await propiedades.UnidadesAsync(User.Id(), new FiltroUnidades(Estado: "Disponible"), cancelacion);
        modelo.Inquilinos = await inquilinos.ActivosAsync(User.Id(), cancelacion);
        modelo.Plantillas = await configuracion.PlantillasAsync(true, cancelacion);
    }
}
