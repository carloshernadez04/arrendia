using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Personal)]
[Route("cargos")]
public sealed class CargosController(
    IRepositorioCobros cobros,
    IRepositorioContratos contratos,
    IRepositorioPropiedades propiedades,
    IRepositorioPermisos permisos,
    IRepositorioArchivos archivos,
    IRepositorioCorreos correos,
    IAlmacenArchivos almacen,
    ServicioDocumentos documentos) : Controller
{
    public static readonly string[] Situaciones = ["Pendientes", "Vencido", "Por vencer", "Pagado", "Condonado", "Anulado"];
    public static readonly string[] Origenes = ["Automatico", "Manual", "Mora", "Reparacion"];

    [HttpGet("")]
    public async Task<IActionResult> Index(string? situacion, int? propiedad, int? contrato, string? origen, string? buscar, CancellationToken cancelacion)
    {
        var filtro = new FiltroCargos(Situaciones.Contains(situacion) ? situacion : null, propiedad, contrato, Origenes.Contains(origen) ? origen : null, buscar);
        return View(new CargosModelo
        {
            Cargos = await cobros.CargosAsync(User.Id(), filtro, cancelacion),
            Propiedades = await propiedades.ListarAsync(User.Id(), cancelacion),
            Filtro = filtro,
            Permisos = await permisos.DeAsync(User.Id(), cancelacion),
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancelacion)
    {
        var cargo = await cobros.CargoAsync(id, User.Id(), cancelacion);
        if (cargo is null)
        {
            return NotFound();
        }

        return View(new CargoDetalleModelo
        {
            Cargo = cargo,
            Ajustes = await cobros.AjustesAsync(id, cancelacion),
            Aplicaciones = await cobros.AplicacionesDeCargoAsync(id, cancelacion),
            ArchivoRespaldoId = await cobros.ArchivoRespaldoAsync(id, cancelacion),
            Permisos = await permisos.DeAsync(User.Id(), cancelacion),
        });
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Nuevo(int? contrato, CancellationToken cancelacion)
    {
        if (!(await permisos.DeAsync(User.Id(), cancelacion)).CargosManuales)
        {
            return Forbid();
        }

        var modelo = new CargoFormulario { ContratoId = contrato, FechaVencimiento = Formato.HoySv().AddDays(10) };
        await OpcionesAsync(modelo, cancelacion);
        return View(modelo);
    }

    [HttpPost("nuevo")]
    public async Task<IActionResult> Nuevo(CargoFormulario modelo, CancellationToken cancelacion)
    {
        if (!(await permisos.DeAsync(User.Id(), cancelacion)).CargosManuales)
        {
            return Forbid();
        }

        if (modelo.FechaVencimiento is { } vence && vence.Date < Formato.HoySv())
        {
            ModelState.AddModelError(nameof(modelo.FechaVencimiento), "El vencimiento no puede ser anterior a hoy.");
        }

        if (modelo.ContratoId is { } contratoId && await contratos.ObtenerAsync(contratoId, User.Id(), cancelacion) is { } contrato
            && contrato.FechaInicio.Date > Formato.HoySv())
        {
            ModelState.AddModelError(nameof(modelo.ContratoId), $"El contrato empieza el {Formato.Fecha(contrato.FechaInicio)}: los cargos manuales se crean desde esa fecha.");
        }

        string? tipo = null;
        if (modelo.Respaldo is { Length: > 0 })
        {
            (tipo, var error) = await TiposArchivo.ValidarAsync(modelo.Respaldo, TiposArchivo.ImagenesYPdf, cancelacion);
            if (error is not null)
            {
                ModelState.AddModelError(nameof(modelo.Respaldo), error);
            }
        }

        if (!ModelState.IsValid)
        {
            await OpcionesAsync(modelo, cancelacion);
            return View(modelo);
        }

        try
        {
            int? archivoId = null;
            if (tipo is not null)
            {
                string publicId;
                await using (var flujo = modelo.Respaldo!.OpenReadStream())
                {
                    publicId = await almacen.GuardarAsync(flujo, tipo, "respaldos", cancelacion);
                }

                archivoId = await archivos.RegistrarAsync(new NuevoArchivo(publicId, modelo.Respaldo.FileName, tipo, modelo.Respaldo.Length, "RespaldoCargo",
                    ContratoId: modelo.ContratoId, UsuarioId: User.Id()), cancelacion);
            }

            var id = await cobros.CrearCargoAsync(new NuevoCargo(modelo.ContratoId!.Value, modelo.ConceptoCodigo!, modelo.Descripcion!.Trim(), modelo.MontoBase!.Value,
                modelo.FechaVencimiento!.Value, Opciones.SiNo(modelo.GeneraMora), archivoId), User.Id(), cancelacion);
            this.Exito("Cargo creado. El aviso de cobro se envía por correo a los contactos del inquilino.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError("", error.Message);
            await OpcionesAsync(modelo, cancelacion);
            return View(modelo);
        }
    }

    [HttpPost("{id:int}/condonar")]
    public async Task<IActionResult> Condonar(int id, decimal? monto, string? motivo, CancellationToken cancelacion)
    {
        var cargo = await cobros.CargoAsync(id, User.Id(), cancelacion);
        if (cargo is null)
        {
            return NotFound();
        }

        if (monto is not > 0 || monto > cargo.Saldo)
        {
            this.Fallo($"El monto a condonar debe ser mayor a $0.00 y no pasar del saldo ({Formato.Dinero(cargo.Saldo)}).");
            return RedirectToAction(nameof(Detalle), new { id });
        }

        try
        {
            await cobros.CondonarAsync(id, monto.Value, motivo?.Trim() ?? "", User.Id(), cancelacion);
            this.Exito($"Se condonaron {Formato.Dinero(monto)}.");
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
        if (await cobros.CargoAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        try
        {
            await cobros.AnularCargoAsync(id, motivo?.Trim() ?? "", User.Id(), cancelacion);
            this.Exito("Cargo anulado (y su mora, si tenía).");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpGet("{id:int}/aviso")]
    public async Task<IActionResult> Aviso(int id, CancellationToken cancelacion)
    {
        var cargo = await cobros.CargoAsync(id, User.Id(), cancelacion);
        if (cargo is null)
        {
            return NotFound();
        }

        if (await correos.DocumentoDeCargoAsync(id, cancelacion) is { } existente
            && await correos.ArchivoDeDocumentoAsync(existente, cancelacion) is { } archivo
            && await almacen.AbrirAsync(archivo.PublicId, archivo.TipoMime, cancelacion) is { } flujo)
        {
            return File(flujo, "application/pdf", $"aviso-{cargo.Contrato}-{id}.pdf");
        }

        var documento = await documentos.AvisoCobroAsync(id, cargo.ContratoId, cancelacion);
        return documento is null ? NotFound() : this.Pdf(documento.Pdf, $"aviso-{cargo.Contrato}-{id}.pdf");
    }

    private async Task OpcionesAsync(CargoFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Contratos = await contratos.ActivosAsync(User.Id(), cancelacion);
        modelo.Conceptos = await cobros.ConceptosManualesAsync(cancelacion);
    }
}

[Authorize(Roles = Roles.Personal)]
[Route("pagos")]
public sealed class PagosController(
    IRepositorioCobros cobros,
    IRepositorioContratos contratos,
    IRepositorioPropiedades propiedades,
    IRepositorioPermisos permisos,
    IRepositorioArchivos archivos,
    IAlmacenArchivos almacen,
    ServicioDocumentos documentos) : Controller
{
    public static readonly string[] Metodos = ["Efectivo", "Transferencia", "Tarjeta"];

    [HttpGet("")]
    public async Task<IActionResult> Index(string? metodo, string? estado, int? propiedad, DateTime? desde, DateTime? hasta, string? buscar, CancellationToken cancelacion)
    {
        var filtro = new FiltroPagos(Metodos.Contains(metodo) ? metodo : null, estado is "Aplicado" or "Anulado" ? estado : null, propiedad, desde, hasta, buscar);
        return View(new PagosModelo
        {
            Pagos = await cobros.PagosAsync(User.Id(), filtro, cancelacion),
            Propiedades = await propiedades.ListarAsync(User.Id(), cancelacion),
            Filtro = filtro,
            Permisos = await permisos.DeAsync(User.Id(), cancelacion),
        });
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Nuevo(int? contrato, CancellationToken cancelacion)
    {
        if (!(await permisos.DeAsync(User.Id(), cancelacion)).RegistrarPagos)
        {
            return Forbid();
        }

        var modelo = new PagoFormulario { ContratoId = contrato };
        await OpcionesAsync(modelo, cancelacion);
        return View(modelo);
    }

    [HttpPost("nuevo")]
    public async Task<IActionResult> Nuevo(PagoFormulario modelo, CancellationToken cancelacion)
    {
        if (!(await permisos.DeAsync(User.Id(), cancelacion)).RegistrarPagos)
        {
            return Forbid();
        }

        await OpcionesAsync(modelo, cancelacion);
        if (modelo.ContratoId is null || modelo.Contratos.All(c => c.Id != modelo.ContratoId))
        {
            ModelState.AddModelError(nameof(modelo.ContratoId), "Elige un contrato activo.");
            return View(modelo);
        }

        if (modelo.Metodo is not ("Efectivo" or "Transferencia"))
        {
            ModelState.AddModelError(nameof(modelo.Metodo), "Elige efectivo o transferencia. Los pagos con tarjeta los hace el inquilino desde el portal.");
        }

        if (modelo.Metodo == "Transferencia" && string.IsNullOrWhiteSpace(modelo.Referencia))
        {
            ModelState.AddModelError(nameof(modelo.Referencia), "Escribe el número de referencia de la transferencia.");
        }

        var elegidos = modelo.Pendientes.Where(c => modelo.Cargos.Contains(c.Id)).ToList();
        var error = ReglasPago.Validar(modelo.Monto, elegidos.Count > 0 ? elegidos : modelo.Pendientes, modelo.PermiteParciales);
        if (error is not null)
        {
            ModelState.AddModelError(nameof(modelo.Monto), error);
        }

        string? tipo = null;
        if (modelo.Comprobante is { Length: > 0 })
        {
            (tipo, var errorArchivo) = await TiposArchivo.ValidarAsync(modelo.Comprobante, TiposArchivo.ImagenesYPdf, cancelacion);
            if (errorArchivo is not null)
            {
                ModelState.AddModelError(nameof(modelo.Comprobante), errorArchivo);
            }
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        try
        {
            int? archivoId = null;
            if (tipo is not null)
            {
                string publicId;
                await using (var flujo = modelo.Comprobante!.OpenReadStream())
                {
                    publicId = await almacen.GuardarAsync(flujo, tipo, "comprobantes", cancelacion);
                }

                archivoId = await archivos.RegistrarAsync(new NuevoArchivo(publicId, modelo.Comprobante.FileName, tipo, modelo.Comprobante.Length,
                    "ComprobanteTransferencia", ContratoId: modelo.ContratoId, UsuarioId: User.Id()), cancelacion);
            }

            var (id, numero) = await cobros.RegistrarPagoAsync(new NuevoPago(modelo.ContratoId.Value, modelo.Monto!.Value, modelo.Metodo,
                modelo.Referencia?.Trim(), elegidos.Count > 0 ? modelo.Cargos : null, archivoId), User.Id(), cancelacion);
            await documentos.ComprobanteAsync(id, User.Id(), cancelacion);
            this.Exito($"Pago {numero} registrado por {Formato.Dinero(modelo.Monto)}. El comprobante se envía por correo al inquilino.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ReglaDeNegocioException ex)
        {
            ModelState.AddModelError(nameof(modelo.Monto), ex.Message);
            return View(modelo);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancelacion)
    {
        var pago = await cobros.PagoAsync(id, User.Id(), cancelacion);
        if (pago is null)
        {
            return NotFound();
        }

        return View(new PagoDetalleModelo
        {
            Pago = pago,
            Aplicaciones = await cobros.AplicacionesDePagoAsync(id, cancelacion),
            Permisos = await permisos.DeAsync(User.Id(), cancelacion),
        });
    }

    [HttpGet("{id:int}/comprobante")]
    public async Task<IActionResult> Comprobante(int id, [FromServices] IRepositorioDocumentos repositorioDocumentos, CancellationToken cancelacion)
    {
        var pago = await cobros.PagoAsync(id, User.Id(), cancelacion);
        if (pago is null)
        {
            return NotFound();
        }

        if (pago.ComprobanteDocumentoId is { } documentoId
            && await repositorioDocumentos.ArchivoAsync(documentoId, cancelacion) is { } archivo
            && await almacen.AbrirAsync(archivo.PublicId, archivo.TipoMime, cancelacion) is { } flujo)
        {
            return File(flujo, "application/pdf", $"comprobante-{pago.Numero}.pdf");
        }

        var documento = await documentos.ComprobanteAsync(id, null, cancelacion);
        return documento is null ? NotFound() : this.Pdf(documento.Pdf, $"comprobante-{pago.Numero}.pdf");
    }

    [HttpPost("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id, string? motivo, CancellationToken cancelacion)
    {
        if (await cobros.PagoAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        try
        {
            await cobros.AnularPagoAsync(id, motivo?.Trim() ?? "", User.Id(), cancelacion);
            this.Exito("Pago anulado. El saldo volvió a los cargos.");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    private async Task OpcionesAsync(PagoFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Contratos = await contratos.ActivosAsync(User.Id(), cancelacion);
        if (modelo.ContratoId is { } contratoId && modelo.Contratos.Any(c => c.Id == contratoId))
        {
            modelo.Pendientes = await cobros.PendientesAsync(contratoId, cancelacion);
            modelo.PermiteParciales = await cobros.PermiteParcialesAsync(contratoId, cancelacion);
        }
    }
}

[Authorize(Roles = Roles.Personal)]
[Route("cobranza")]
public sealed class CobranzaController(IRepositorioCobros cobros, IRepositorioPropiedades propiedades) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? propiedad, int? dias, CancellationToken cancelacion)
    {
        var minimo = dias is 0 or 1 or 31 or 61 or 91 ? dias.Value : 1;
        return View(new CobranzaModelo
        {
            Filas = await cobros.CobranzaAsync(User.Id(), propiedad, minimo, cancelacion),
            Propiedades = await propiedades.ListarAsync(User.Id(), cancelacion),
            PropiedadId = propiedad,
            MinimoDias = minimo,
        });
    }
}
