using System.Globalization;
using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Administrador)]
[Route("configuracion")]
public sealed class ConfiguracionController(IRepositorioConfiguracion repositorio) : Controller
{
    public static readonly (string Clave, string Texto)[] Secciones =
    [
        ("plantillas", "Condiciones de contrato"),
        ("conceptos", "Conceptos de cobro"),
        ("catalogos", "Catálogos"),
        ("feriados", "Feriados"),
        ("parametros", "Parámetros"),
    ];

    [HttpGet("")]
    public async Task<IActionResult> Index(string? seccion, CancellationToken cancelacion)
    {
        seccion = Secciones.Any(s => s.Clave == seccion) ? seccion! : "plantillas";
        var modelo = new ConfiguracionModelo { Seccion = seccion };
        switch (seccion)
        {
            case "plantillas":
                modelo.Plantillas = await repositorio.PlantillasAsync(false, cancelacion);
                break;
            case "conceptos":
                modelo.Conceptos = await repositorio.ConceptosAsync(cancelacion);
                break;
            case "catalogos":
                modelo.TiposUnidad = await repositorio.CatalogoAsync("TIPO_UNIDAD", cancelacion);
                modelo.Especialidades = await repositorio.CatalogoAsync("ESPECIALIDAD", cancelacion);
                modelo.Categorias = await repositorio.CatalogoAsync("CATEGORIA_REPORTE", cancelacion);
                break;
            case "feriados":
                modelo.Feriados = await repositorio.FeriadosAsync(cancelacion);
                break;
            default:
                modelo.Parametros = await repositorio.ParametrosAsync(cancelacion);
                break;
        }

        return View(modelo);
    }

    [HttpGet("plantillas/nueva")]
    public IActionResult NuevaPlantilla() => View("Plantilla", new Plantilla());

    [HttpGet("plantillas/{id:int}")]
    public async Task<IActionResult> EditarPlantilla(int id, CancellationToken cancelacion)
    {
        var plantilla = await repositorio.PlantillaAsync(id, cancelacion);
        return plantilla is null ? NotFound() : View("Plantilla", plantilla);
    }

    [HttpPost("plantillas/guardar")]
    public async Task<IActionResult> GuardarPlantilla(Plantilla plantilla, CancellationToken cancelacion)
    {
        plantilla.Nombre = plantilla.Nombre?.Trim() ?? "";
        if (plantilla.Nombre.Length is 0 or > 80)
        {
            ModelState.AddModelError(nameof(plantilla.Nombre), "Escribe un nombre (máximo 80 caracteres).");
        }

        if (plantilla.DiaPago is < 1 or > 31)
        {
            ModelState.AddModelError(nameof(plantilla.DiaPago), "El día de pago va del 1 al 31.");
        }

        if (plantilla.DiasEmisionAviso is < 0 or > 45)
        {
            ModelState.AddModelError(nameof(plantilla.DiasEmisionAviso), "El aviso se emite entre 0 y 45 días antes.");
        }

        if (plantilla.DiasGracia is < 0 or > 60)
        {
            ModelState.AddModelError(nameof(plantilla.DiasGracia), "Los días de gracia van de 0 a 60.");
        }

        if (plantilla.PorcentajeMoraDiario is < 0 or > 5)
        {
            ModelState.AddModelError(nameof(plantilla.PorcentajeMoraDiario), "La mora diaria va de 0 % a 5 %.");
        }

        if (plantilla.TipoTopeMora is not ("Ninguno" or "Monto" or "PorcentajeCargo"))
        {
            ModelState.AddModelError(nameof(plantilla.TipoTopeMora), "Elige el tipo de tope.");
        }
        else if (plantilla.TipoTopeMora != "Ninguno" && plantilla.ValorTopeMora is not > 0)
        {
            ModelState.AddModelError(nameof(plantilla.ValorTopeMora), "Escribe el valor del tope.");
        }

        if (!ModelState.IsValid)
        {
            return View("Plantilla", plantilla);
        }

        try
        {
            await repositorio.GuardarPlantillaAsync(plantilla, User.Id(), cancelacion);
            this.Exito($"Plantilla «{plantilla.Nombre}» guardada. Se aplica a los contratos nuevos.");
            return RedirectToAction(nameof(Index), new { seccion = "plantillas" });
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError(nameof(plantilla.Nombre), error.Message);
            return View("Plantilla", plantilla);
        }
    }

    [HttpPost("plantillas/{id:int}/predeterminada")]
    public async Task<IActionResult> Predeterminada(int id, CancellationToken cancelacion)
    {
        await repositorio.HacerPredeterminadaAsync(id, User.Id(), cancelacion);
        this.Exito("Plantilla predeterminada actualizada.");
        return RedirectToAction(nameof(Index), new { seccion = "plantillas" });
    }

    [HttpPost("conceptos")]
    public async Task<IActionResult> Concepto(Concepto concepto, CancellationToken cancelacion)
    {
        concepto.Codigo = (concepto.Codigo ?? "").Trim().ToUpperInvariant();
        concepto.Nombre = (concepto.Nombre ?? "").Trim();
        if (concepto.Nombre.Length is 0 or > 60 || (concepto.Id == 0 && !EsCodigo(concepto.Codigo)) || concepto.AplicaIva is not ("Si" or "No" or "SegunUnidad"))
        {
            this.Fallo("Revisa el concepto: código de 2 a 20 letras o números sin espacios, nombre de hasta 60 caracteres y si aplica IVA.");
        }
        else
        {
            try
            {
                await repositorio.GuardarConceptoAsync(concepto, User.Id(), cancelacion);
                this.Exito($"Concepto «{concepto.Nombre}» guardado.");
            }
            catch (ReglaDeNegocioException error)
            {
                this.Fallo(error.Message);
            }
        }

        return RedirectToAction(nameof(Index), new { seccion = "conceptos" });
    }

    [HttpPost("catalogos/{tabla}")]
    public async Task<IActionResult> Catalogo(string tabla, ElementoCatalogo elemento, CancellationToken cancelacion)
    {
        if (!RepositorioConfiguracion.Catalogos.Contains(tabla))
        {
            return NotFound();
        }

        elemento.Nombre = (elemento.Nombre ?? "").Trim();
        if (elemento.Nombre.Length is 0 or > 60)
        {
            this.Fallo("Escribe un nombre de hasta 60 caracteres.");
        }
        else
        {
            if (elemento.Id == 0)
            {
                elemento.Activo = true;
            }

            try
            {
                await repositorio.GuardarCatalogoAsync(tabla, elemento, cancelacion);
                this.Exito($"«{elemento.Nombre}» guardado.");
            }
            catch (ReglaDeNegocioException error)
            {
                this.Fallo(error.Message);
            }
        }

        return RedirectToAction(nameof(Index), new { seccion = "catalogos" });
    }

    [HttpPost("feriados")]
    public async Task<IActionResult> Feriado(DateTime? fecha, string? nombre, CancellationToken cancelacion)
    {
        nombre = nombre?.Trim();
        if (fecha is null || string.IsNullOrEmpty(nombre) || nombre.Length > 80)
        {
            this.Fallo("Escribe la fecha y el nombre del feriado.");
        }
        else
        {
            try
            {
                await repositorio.AgregarFeriadoAsync(fecha.Value, nombre, cancelacion);
                this.Exito("Feriado agregado. Los vencimientos que caigan ese día se correrán al siguiente día hábil.");
            }
            catch (ReglaDeNegocioException error)
            {
                this.Fallo(error.Message);
            }
        }

        return RedirectToAction(nameof(Index), new { seccion = "feriados" });
    }

    [HttpPost("feriados/{id:int}/quitar")]
    public async Task<IActionResult> QuitarFeriado(int id, CancellationToken cancelacion)
    {
        await repositorio.QuitarFeriadoAsync(id, cancelacion);
        this.Exito("Feriado quitado.");
        return RedirectToAction(nameof(Index), new { seccion = "feriados" });
    }

    [HttpPost("parametros")]
    public async Task<IActionResult> Parametro(string clave, string? valor, CancellationToken cancelacion)
    {
        var parametro = (await repositorio.ParametrosAsync(cancelacion)).FirstOrDefault(p => p.Clave == clave);
        if (parametro is null)
        {
            return NotFound();
        }

        valor = valor?.Trim() ?? "";
        var error = parametro.Tipo switch
        {
            "Numero" when !decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) || n < 0 => "Escribe un número mayor o igual a 0.",
            "Bool" when valor is not ("true" or "false") => "Elige sí o no.",
            "Fecha" when !DateTime.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) => "Escribe una fecha válida.",
            _ when valor.Length is 0 or > 500 => "Escribe un valor (máximo 500 caracteres).",
            _ => null,
        };
        if (error is not null)
        {
            this.Fallo($"{parametro.Clave}: {error}");
        }
        else
        {
            await repositorio.GuardarParametroAsync(clave, valor, User.Id(), cancelacion);
            this.Exito($"Parámetro {parametro.Clave} actualizado.");
        }

        return RedirectToAction(nameof(Index), new { seccion = "parametros" });
    }

    private static bool EsCodigo(string codigo) => codigo.Length is >= 2 and <= 20 && codigo.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
