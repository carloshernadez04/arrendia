using Arrendia.Web.Data;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Personal)]
[Route("unidades")]
public sealed class UnidadesController(IRepositorioPropiedades repositorio) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? propiedad, string? estado, int? tipo, string? buscar, CancellationToken cancelacion)
    {
        var filtro = new FiltroUnidades(propiedad, Catalogos.EstadosUnidad.Contains(estado) ? estado : null, tipo, buscar);
        return View(new UnidadesModelo
        {
            Unidades = await repositorio.UnidadesAsync(User.Id(), filtro, cancelacion),
            Propiedades = await repositorio.ListarAsync(User.Id(), cancelacion),
            Tipos = await repositorio.TiposDeUnidadAsync(cancelacion),
            Filtro = filtro,
            PuedeEditar = User.EsAdministrador(),
        });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet("nueva")]
    public async Task<IActionResult> Nueva(int? propiedad, string? volver, CancellationToken cancelacion)
    {
        var modelo = new UnidadFormulario { Volver = volver, Tipos = await repositorio.TiposDeUnidadAsync(cancelacion) };
        if (propiedad is { } id)
        {
            var p = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
            if (p is null)
            {
                return NotFound();
            }

            modelo.PropiedadId = p.Id;
            modelo.PropiedadNombre = p.Nombre;
            modelo.AplicaIva = p.Tipo != "Residencial";
        }
        else
        {
            modelo.Propiedades = await repositorio.ListarAsync(User.Id(), cancelacion);
        }

        return View("Formulario", modelo);
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("nueva")]
    public async Task<IActionResult> Nueva(UnidadFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Tipos = await repositorio.TiposDeUnidadAsync(cancelacion);
        var p = modelo.PropiedadId > 0 ? await repositorio.ObtenerAsync(modelo.PropiedadId, User.Id(), cancelacion) : null;
        if (p is null)
        {
            ModelState.AddModelError(nameof(modelo.PropiedadId), "Elige la propiedad.");
            modelo.Propiedades = await repositorio.ListarAsync(User.Id(), cancelacion);
        }
        else
        {
            modelo.PropiedadNombre = p.Nombre;
        }

        if (!Validar(modelo))
        {
            return View("Formulario", modelo);
        }

        try
        {
            await repositorio.CrearUnidadAsync(Datos(modelo), User.Id(), cancelacion);
            TempData["Mensaje"] = $"Unidad {modelo.Codigo!.Trim()} agregada.";
            return Regresar(modelo.Volver, modelo.PropiedadId);
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError(nameof(modelo.Codigo), error.Message);
            return View("Formulario", modelo);
        }
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpGet("{id:int}/editar")]
    public async Task<IActionResult> Editar(int id, string? volver, CancellationToken cancelacion)
    {
        var u = await repositorio.UnidadAsync(id, User.Id(), cancelacion);
        if (u is null)
        {
            return NotFound();
        }

        return View("Formulario", new UnidadFormulario
        {
            Id = u.Id,
            PropiedadId = u.PropiedadId,
            PropiedadNombre = u.Propiedad,
            Codigo = u.Codigo,
            TipoUnidadId = u.TipoUnidadId,
            Nivel = u.Nivel,
            AreaM2 = u.AreaM2,
            RentaReferencia = u.RentaReferencia,
            AplicaIva = u.AplicaIva,
            Volver = volver,
            Tipos = await repositorio.TiposDeUnidadAsync(cancelacion),
        });
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("{id:int}/editar")]
    public async Task<IActionResult> Editar(int id, UnidadFormulario modelo, CancellationToken cancelacion)
    {
        var u = await repositorio.UnidadAsync(id, User.Id(), cancelacion);
        if (u is null)
        {
            return NotFound();
        }

        modelo.Id = id;
        modelo.PropiedadId = u.PropiedadId;
        modelo.PropiedadNombre = u.Propiedad;
        modelo.Tipos = await repositorio.TiposDeUnidadAsync(cancelacion);
        if (!Validar(modelo))
        {
            return View("Formulario", modelo);
        }

        try
        {
            await repositorio.ActualizarUnidadAsync(id, Datos(modelo), User.Id(), cancelacion);
            TempData["Mensaje"] = $"Unidad {modelo.Codigo!.Trim()} actualizada.";
            return Regresar(modelo.Volver, u.PropiedadId);
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError(nameof(modelo.Codigo), error.Message);
            return View("Formulario", modelo);
        }
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("{id:int}/estado")]
    public async Task<IActionResult> Estado(int id, string estado, string? volver, CancellationToken cancelacion)
    {
        var u = await repositorio.UnidadAsync(id, User.Id(), cancelacion);
        if (u is null)
        {
            return NotFound();
        }

        try
        {
            await repositorio.CambiarEstadoUnidadAsync(id, estado, User.Id(), cancelacion);
            TempData["Mensaje"] = $"La unidad {u.Codigo} quedó {Catalogos.TextoEstado(estado).ToLowerInvariant()}.";
        }
        catch (ReglaDeNegocioException error)
        {
            TempData["Error"] = error.Message;
        }

        return Regresar(volver, u.PropiedadId);
    }

    private IActionResult Regresar(string? volver, int propiedadId) =>
        volver is not null && Url.IsLocalUrl(volver) && volver.StartsWith("/unidades", StringComparison.Ordinal)
            ? LocalRedirect(volver)
            : RedirectToAction("Detalle", "Propiedades", new { id = propiedadId });

    private bool Validar(UnidadFormulario modelo)
    {
        if (modelo.TipoUnidadId is { } tipo && modelo.Tipos.All(t => t.Id != tipo))
        {
            ModelState.AddModelError(nameof(modelo.TipoUnidadId), "Elige un tipo de la lista.");
        }

        return ModelState.IsValid;
    }

    private static DatosUnidad Datos(UnidadFormulario m) =>
        new(m.PropiedadId, m.Codigo!.Trim(), m.TipoUnidadId!.Value, string.IsNullOrWhiteSpace(m.Nivel) ? null : m.Nivel.Trim(),
            m.AreaM2, m.RentaReferencia, m.AplicaIva);
}
