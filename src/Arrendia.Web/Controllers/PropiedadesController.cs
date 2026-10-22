using Arrendia.Web.Data;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Personal)]
[Route("propiedades")]
public sealed class PropiedadesController(IRepositorioPropiedades repositorio) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancelacion) =>
        View(await repositorio.ListarAsync(User.Id(), cancelacion));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancelacion)
    {
        var propiedad = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (propiedad is null)
        {
            return NotFound();
        }

        var unidades = await repositorio.UnidadesAsync(User.Id(), new FiltroUnidades(PropiedadId: id), cancelacion);
        return View(new PropiedadDetalleModelo { Propiedad = propiedad, Unidades = unidades, PuedeEditar = User.EsAdministrador() });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet("nueva")]
    public async Task<IActionResult> Nueva(CancellationToken cancelacion) =>
        await FormularioAsync(new PropiedadFormulario(), cancelacion);

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("nueva")]
    public async Task<IActionResult> Nueva(PropiedadFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Id = null;
        modelo.Tipos = await repositorio.TiposDeUnidadAsync(cancelacion);
        var datos = Validar(modelo);
        var unidad = modelo.UnaSolaUnidad ? ValidarUnidad(modelo) : null;
        if (datos is null || (modelo.UnaSolaUnidad && unidad is null))
        {
            return View("Formulario", modelo);
        }

        try
        {
            if (unidad is not null)
            {
                var id = await repositorio.CrearConUnidadAsync(datos, unidad, User.Id(), cancelacion);
                TempData["Mensaje"] = $"«{datos.Nombre}» registrada como unidad única. Ya puedes hacerle un contrato.";
                return RedirectToAction(nameof(Detalle), new { id });
            }

            var nuevaId = await repositorio.CrearAsync(datos, User.Id(), cancelacion);
            TempData["Mensaje"] = $"Propiedad «{datos.Nombre}» creada. Ahora agrega sus unidades.";
            return RedirectToAction(nameof(Detalle), new { id = nuevaId });
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError(nameof(modelo.Nombre), error.Message);
            return View("Formulario", modelo);
        }
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet("{id:int}/editar")]
    public async Task<IActionResult> Editar(int id, CancellationToken cancelacion)
    {
        var p = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        return p is null
            ? NotFound()
            : await FormularioAsync(new PropiedadFormulario
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Tipo = p.Tipo,
                Direccion = p.Direccion,
                Municipio = p.Municipio,
                Departamento = p.Departamento,
                Ubicacion = UbicacionMapa.Texto(p.Latitud, p.Longitud),
            }, cancelacion);
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("{id:int}/editar")]
    public async Task<IActionResult> Editar(int id, PropiedadFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Id = id;
        modelo.UnaSolaUnidad = false;
        var datos = Validar(modelo);
        if (datos is null)
        {
            return await FormularioAsync(modelo, cancelacion);
        }

        if (await repositorio.ObtenerAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        try
        {
            await repositorio.ActualizarAsync(id, datos, User.Id(), cancelacion);
            TempData["Mensaje"] = "Cambios guardados.";
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError(nameof(modelo.Nombre), error.Message);
            return await FormularioAsync(modelo, cancelacion);
        }
    }

    private async Task<IActionResult> FormularioAsync(PropiedadFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Tipos = await repositorio.TiposDeUnidadAsync(cancelacion);
        return View("Formulario", modelo);
    }

    private DatosPropiedad? Validar(PropiedadFormulario modelo)
    {
        if (modelo.Tipo is not null && !Catalogos.TiposPropiedad.Contains(modelo.Tipo))
        {
            ModelState.AddModelError(nameof(modelo.Tipo), "Elige un tipo de la lista.");
        }

        if (modelo.Departamento is not null && !Territorio.Municipios.ContainsKey(modelo.Departamento))
        {
            ModelState.AddModelError(nameof(modelo.Departamento), "Elige un departamento de la lista.");
        }
        else if (modelo.Municipio is not null && !Territorio.Existe(modelo.Departamento, modelo.Municipio))
        {
            ModelState.AddModelError(nameof(modelo.Municipio), $"Elige un municipio de {modelo.Departamento}.");
        }

        var ubicacion = UbicacionMapa.Interpretar(modelo.Ubicacion);
        if (ubicacion.Error is not null)
        {
            ModelState.AddModelError(nameof(modelo.Ubicacion), ubicacion.Error);
        }

        if (!modelo.UnaSolaUnidad)
        {
            foreach (var campo in new[] { nameof(modelo.AreaM2), nameof(modelo.RentaReferencia), nameof(modelo.TipoUnidadId) })
            {
                ModelState.Remove(campo);
            }
        }

        return ModelState.IsValid
            ? new DatosPropiedad(modelo.Nombre!.Trim(), modelo.Tipo!, modelo.Direccion!.Trim(), modelo.Municipio!, modelo.Departamento!,
                ubicacion.Latitud, ubicacion.Longitud)
            : null;
    }

    private DatosUnidad? ValidarUnidad(PropiedadFormulario modelo)
    {
        if (modelo.TipoUnidadId is not { } tipo)
        {
            ModelState.AddModelError(nameof(modelo.TipoUnidadId), "Elige el tipo de unidad.");
        }
        else if (modelo.Tipos.All(t => t.Id != tipo))
        {
            ModelState.AddModelError(nameof(modelo.TipoUnidadId), "Elige un tipo de la lista.");
        }

        return ModelState.IsValid
            ? new DatosUnidad(0, Catalogos.CodigoUnidadUnica, modelo.TipoUnidadId!.Value, null, modelo.AreaM2, modelo.RentaReferencia, modelo.AplicaIva)
            : null;
    }
}
