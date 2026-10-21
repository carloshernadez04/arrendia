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
    public IActionResult Nueva() => View("Formulario", new PropiedadFormulario());

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("nueva")]
    public async Task<IActionResult> Nueva(PropiedadFormulario modelo, CancellationToken cancelacion)
    {
        var datos = Validar(modelo);
        if (datos is null)
        {
            return View("Formulario", modelo);
        }

        try
        {
            var id = await repositorio.CrearAsync(datos, User.Id(), cancelacion);
            TempData["Mensaje"] = $"Propiedad «{datos.Nombre}» creada. Ahora agrega sus unidades.";
            return RedirectToAction(nameof(Detalle), new { id });
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
            : View("Formulario", new PropiedadFormulario
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Tipo = p.Tipo,
                Direccion = p.Direccion,
                Municipio = p.Municipio,
                Departamento = p.Departamento,
                Ubicacion = UbicacionMapa.Texto(p.Latitud, p.Longitud),
            });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("{id:int}/editar")]
    public async Task<IActionResult> Editar(int id, PropiedadFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Id = id;
        var datos = Validar(modelo);
        if (datos is null)
        {
            return View("Formulario", modelo);
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
            return View("Formulario", modelo);
        }
    }

    private DatosPropiedad? Validar(PropiedadFormulario modelo)
    {
        if (modelo.Tipo is not null && !Catalogos.TiposPropiedad.Contains(modelo.Tipo))
        {
            ModelState.AddModelError(nameof(modelo.Tipo), "Elige un tipo de la lista.");
        }

        if (modelo.Departamento is not null && !Catalogos.Departamentos.Contains(modelo.Departamento))
        {
            ModelState.AddModelError(nameof(modelo.Departamento), "Elige un departamento de la lista.");
        }

        var ubicacion = UbicacionMapa.Interpretar(modelo.Ubicacion);
        if (ubicacion.Error is not null)
        {
            ModelState.AddModelError(nameof(modelo.Ubicacion), ubicacion.Error);
        }

        return ModelState.IsValid
            ? new DatosPropiedad(modelo.Nombre!.Trim(), modelo.Tipo!, modelo.Direccion!.Trim(), modelo.Municipio!.Trim(), modelo.Departamento!,
                ubicacion.Latitud, ubicacion.Longitud)
            : null;
    }
}
