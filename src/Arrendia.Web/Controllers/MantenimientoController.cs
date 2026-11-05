using System.Globalization;
using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Personal)]
[Route("tecnicos")]
public sealed class TecnicosController(IRepositorioMantenimiento repositorio, IRepositorioPermisos permisos) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancelacion) => View(new TecnicosModelo
    {
        Tecnicos = await repositorio.TecnicosAsync(false, cancelacion),
        PuedeEditar = (await permisos.DeAsync(User.Id(), cancelacion)).GestionarReportes,
    });

    [HttpGet("nuevo")]
    public async Task<IActionResult> Nuevo(CancellationToken cancelacion) =>
        await PuedeAsync(cancelacion) ? View("Formulario", new TecnicoFormulario { Opciones = await repositorio.EspecialidadesAsync(cancelacion) }) : Forbid();

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Editar(int id, CancellationToken cancelacion)
    {
        if (!await PuedeAsync(cancelacion))
        {
            return Forbid();
        }

        var t = await repositorio.TecnicoAsync(id, cancelacion);
        return t is null
            ? NotFound()
            : View("Formulario", new TecnicoFormulario
            {
                Id = t.Id,
                Nombre = t.Nombre,
                Email = t.Email,
                Telefono = t.Telefono,
                Activo = t.Activo,
                Especialidades = [.. await repositorio.EspecialidadesDeAsync(id, cancelacion)],
                Opciones = await repositorio.EspecialidadesAsync(cancelacion),
            });
    }

    [HttpPost("guardar")]
    public async Task<IActionResult> Guardar(TecnicoFormulario modelo, CancellationToken cancelacion)
    {
        if (!await PuedeAsync(cancelacion))
        {
            return Forbid();
        }

        var telefono = ReglasInquilino.NormalizarTelefono(modelo.Telefono);
        if (telefono is null)
        {
            ModelState.AddModelError(nameof(modelo.Telefono), "El teléfono debe tener 8 dígitos.");
        }

        var email = string.IsNullOrWhiteSpace(modelo.Email) ? null : modelo.Email.Trim().ToLowerInvariant();
        if (email is not null && !ReglasInquilino.EmailValido(email))
        {
            ModelState.AddModelError(nameof(modelo.Email), "Escribe un correo válido o déjalo en blanco.");
        }

        if (modelo.Especialidades.Count == 0)
        {
            ModelState.AddModelError(nameof(modelo.Especialidades), "Elige al menos una especialidad.");
        }

        if (!ModelState.IsValid)
        {
            modelo.Opciones = await repositorio.EspecialidadesAsync(cancelacion);
            return View("Formulario", modelo);
        }

        await repositorio.GuardarTecnicoAsync(modelo.Id, new DatosTecnico(modelo.Nombre!.Trim(), email, telefono!, modelo.Activo, modelo.Especialidades), User.Id(), cancelacion);
        this.Exito($"Técnico «{modelo.Nombre.Trim()}» guardado.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> PuedeAsync(CancellationToken cancelacion) => (await permisos.DeAsync(User.Id(), cancelacion)).GestionarReportes;
}

[Authorize(Roles = Roles.Personal)]
[Route("reportes")]
public sealed class ReportesController(
    IRepositorioMantenimiento repositorio,
    IRepositorioPropiedades propiedades,
    IRepositorioArchivos archivos,
    IRepositorioPermisos permisos,
    IAlmacenArchivos almacen,
    EnvioInmediato envio,
    ServicioDocumentos documentos) : Controller
{
    public static readonly string[] EstadosReporte = ["Pendientes", "Abierto", "Asignado", "Terminado", "Cerrado", "Cancelado"];

    [HttpGet("")]
    public async Task<IActionResult> Index(string? estado, string? prioridad, int? propiedad, string? buscar, CancellationToken cancelacion)
    {
        var filtro = new FiltroReportes(EstadosReporte.Contains(estado) ? estado : estado is null ? "Pendientes" : null,
            Prioridades.Todas.Contains(prioridad) ? prioridad : null, propiedad, buscar);
        return View(new ReportesModelo
        {
            Reportes = await repositorio.ReportesAsync(User.Id(), filtro, cancelacion),
            Propiedades = await propiedades.ListarAsync(User.Id(), cancelacion),
            Filtro = filtro,
        });
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Nuevo(CancellationToken cancelacion)
    {
        var modelo = new ReporteFormulario();
        await OpcionesAsync(modelo, cancelacion);
        return View(modelo);
    }

    [HttpPost("nuevo")]
    [RequestSizeLimit(TiposArchivo.TamanoEnvio)]
    public async Task<IActionResult> Nuevo(ReporteFormulario modelo, CancellationToken cancelacion)
    {
        await OpcionesAsync(modelo, cancelacion);
        var lugar = Lugar.Interpretar(modelo.Lugar, modelo.Unidades, modelo.Propiedades);
        if (lugar is null)
        {
            ModelState.AddModelError(nameof(modelo.Lugar), "Elige dónde está el problema.");
        }
        else if (lugar.Value.UnidadId is null && string.IsNullOrWhiteSpace(modelo.AreaComun))
        {
            ModelState.AddModelError(nameof(modelo.AreaComun), "Escribe en qué área común está el problema.");
        }

        if (!Prioridades.Todas.Contains(modelo.Prioridad))
        {
            ModelState.AddModelError(nameof(modelo.Prioridad), "Elige la prioridad.");
        }

        var fotos = await ValidarFotosAsync(modelo.Fotos, ModelState, cancelacion);
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        try
        {
            var (id, numero, _) = await repositorio.CrearReporteAsync(new NuevoReporte(lugar!.Value.PropiedadId, lugar.Value.UnidadId,
                lugar.Value.UnidadId is null ? modelo.AreaComun!.Trim() : null, modelo.CategoriaId!.Value, modelo.Prioridad, modelo.Titulo!.Trim(),
                modelo.Descripcion!.Trim()), User.Id(), cancelacion);
            await GuardarFotosAsync(fotos, id, User.Id(), null, "FotoReporte", archivos, almacen, cancelacion);
            this.Exito($"Reporte {numero} creado. Ahora asígnale un técnico.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError("", error.Message);
            return View(modelo);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancelacion)
    {
        var reporte = await repositorio.ReporteAsync(id, User.Id(), cancelacion);
        if (reporte is null)
        {
            return NotFound();
        }

        return View(new ReporteDetalleModelo
        {
            Reporte = reporte,
            Historial = await repositorio.HistorialAsync(id, cancelacion),
            Fotos = await archivos.DeReporteAsync(id, cancelacion),
            Tecnicos = await repositorio.TecnicosParaAsync(reporte.EspecialidadId, cancelacion),
            Permisos = await permisos.DeAsync(User.Id(), cancelacion),
            Pin = reporte.UnidadId is null && User.IsInRole(Roles.Supervisor) ? await repositorio.PinAsync(id, User.Id(), cancelacion) : null,
        });
    }

    [HttpPost("{id:int}/asignar")]
    public async Task<IActionResult> Asignar(int id, int tecnicoId, bool enviarCorreo, CancellationToken cancelacion)
    {
        var reporte = await repositorio.ReporteAsync(id, User.Id(), cancelacion);
        var tecnico = await repositorio.TecnicoAsync(tecnicoId, cancelacion);
        if (reporte is null || tecnico is null)
        {
            return NotFound();
        }

        var porCorreo = enviarCorreo && tecnico.Email is not null && envio.PuedeEnviar;
        var token = TokenSeguro.Generar();
        try
        {
            await repositorio.AsignarAsync(id, tecnicoId, TokenSeguro.Hash(token), porCorreo, User.Id(), cancelacion);
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var enlace = $"{documentos.UrlPublica()}/t/{token}";
        var enviado = porCorreo && await envio.EnviarAsync("EnlaceTecnico", null,
            PlantillasCorreo.EnlaceTecnico(tecnico.Email!, tecnico.Nombre, reporte.Numero, reporte.Titulo, $"{reporte.Propiedad}, {reporte.Ubicacion}. {reporte.Direccion}", enlace, 48),
            cancelacion);
        if (enviado)
        {
            this.Exito($"Trabajo asignado a {tecnico.Nombre}. Le enviamos el enlace a {tecnico.Email}.");
        }
        else
        {
            this.EnlaceParaCopiar($"Trabajo asignado a {tecnico.Nombre}. Envíale este enlace por WhatsApp o mensaje ({tecnico.Telefono}); vence en 48 horas:", enlace);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/revocar")]
    public async Task<IActionResult> Revocar(int id, CancellationToken cancelacion) => await EjecutarAsync(id,
        () => repositorio.RevocarEnlaceAsync(id, User.Id(), cancelacion), "Enlace del técnico revocado.", cancelacion);

    [HttpPost("{id:int}/estado")]
    public async Task<IActionResult> Estado(int id, string estado, string? comentario, CancellationToken cancelacion) => await EjecutarAsync(id,
        () => repositorio.CambiarEstadoAsync(id, estado, string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(), User.Id(), cancelacion),
        $"El reporte pasó a {estado.ToLowerInvariant()}.", cancelacion);

    [HttpPost("{id:int}/costo")]
    public async Task<IActionResult> Costo(int id, decimal? costo, bool cobrar, CancellationToken cancelacion)
    {
        if (costo is null or < 0)
        {
            this.Fallo("Escribe el costo (puede ser $0.00).");
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return await EjecutarAsync(id, () => repositorio.RegistrarCostoAsync(id, costo.Value, cobrar, User.Id(), cancelacion),
            cobrar ? $"Costo registrado y cobrado al inquilino ({Formato.Dinero(costo)} más IVA)." : "Costo registrado.", cancelacion);
    }

    [HttpPost("{id:int}/fotos")]
    [RequestSizeLimit(TiposArchivo.TamanoEnvio)]
    public async Task<IActionResult> Fotos(int id, List<IFormFile> fotos, CancellationToken cancelacion)
    {
        if (await repositorio.ReporteAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        var validas = await ValidarFotosAsync(fotos, ModelState, cancelacion);
        if (!ModelState.IsValid || validas.Count == 0)
        {
            this.Fallo(ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Elige al menos una foto.");
        }
        else
        {
            await GuardarFotosAsync(validas, id, User.Id(), null, "FotoReporte", archivos, almacen, cancelacion);
            this.Exito(validas.Count == 1 ? "Foto agregada." : $"{validas.Count} fotos agregadas.");
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    public static async Task<List<(IFormFile Archivo, string Tipo)>> ValidarFotosAsync(List<IFormFile> fotos,
        Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary estado, CancellationToken cancelacion)
    {
        var validas = new List<(IFormFile, string)>();
        if (fotos.Count > 6)
        {
            estado.AddModelError("Fotos", "Sube hasta 6 fotos a la vez.");
            return validas;
        }

        foreach (var foto in fotos.Where(f => f.Length > 0))
        {
            var (tipo, error) = await TiposArchivo.ValidarAsync(foto, TiposArchivo.Imagenes, cancelacion);
            if (error is not null)
            {
                estado.AddModelError("Fotos", $"{foto.FileName}: {error}");
            }
            else
            {
                validas.Add((foto, tipo!));
            }
        }

        return validas;
    }

    public static async Task GuardarFotosAsync(List<(IFormFile Archivo, string Tipo)> fotos, int reporteId, int? usuarioId, int? enlaceId, string categoria,
        IRepositorioArchivos archivos, IAlmacenArchivos almacen, CancellationToken cancelacion)
    {
        foreach (var (archivo, tipo) in fotos)
        {
            string publicId;
            await using (var flujo = archivo.OpenReadStream())
            {
                publicId = await almacen.GuardarAsync(flujo, tipo, "reportes", cancelacion);
            }

            await archivos.RegistrarAsync(new NuevoArchivo(publicId, archivo.FileName, tipo, archivo.Length, categoria,
                ReporteId: reporteId, UsuarioId: usuarioId, EnlaceId: enlaceId), cancelacion);
        }
    }

    private async Task<IActionResult> EjecutarAsync(int id, Func<Task> accion, string mensaje, CancellationToken cancelacion)
    {
        if (await repositorio.ReporteAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        try
        {
            await accion();
            this.Exito(mensaje);
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    private async Task OpcionesAsync(ReporteFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Propiedades = await propiedades.ListarAsync(User.Id(), cancelacion);
        modelo.Unidades = await propiedades.UnidadesAsync(User.Id(), new FiltroUnidades(), cancelacion);
        modelo.Categorias = await repositorio.CategoriasAsync(cancelacion);
    }
}

public static class Lugar
{
    public static (int PropiedadId, int? UnidadId)? Interpretar(string? valor, IReadOnlyList<UnidadDetalle> unidades, IReadOnlyList<PropiedadResumen> propiedades)
    {
        if (valor is null || valor.Length < 3 || !int.TryParse(valor.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return null;
        }

        if (valor.StartsWith("u:", StringComparison.Ordinal) && unidades.FirstOrDefault(u => u.Id == id) is { } unidad)
        {
            return (unidad.PropiedadId, unidad.Id);
        }

        return valor.StartsWith("p:", StringComparison.Ordinal) && propiedades.Any(p => p.Id == id) ? (id, null) : null;
    }
}

[AllowAnonymous]
[Route("t/{token}")]
public sealed class TecnicoController(IRepositorioMantenimiento repositorio, IRepositorioArchivos archivos, IAlmacenArchivos almacen) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string token, CancellationToken cancelacion) => View(await ModeloAsync(token, null, cancelacion));

    [HttpPost("pin")]
    [EnableRateLimiting(CuentaController.PoliticaLimite)]
    public async Task<IActionResult> Pin(string token, string? pin, CancellationToken cancelacion)
    {
        if (!TokenSeguro.TieneFormatoValido(token))
        {
            return View("Index", await ModeloAsync(token, null, cancelacion));
        }

        pin = pin?.Trim();
        if (pin is null || pin.Length != 4 || !pin.All(char.IsAsciiDigit))
        {
            return View("Index", await ModeloAsync(token, "Escribe los 4 dígitos del PIN.", cancelacion));
        }

        try
        {
            var resultado = await repositorio.ValidarPinAsync(TokenSeguro.Hash(token), pin, cancelacion);
            var mensaje = resultado.Correcto ? null
                : resultado.Bloqueado ? "El enlace se bloqueó por demasiados intentos. Pide uno nuevo a la administración."
                : $"PIN incorrecto. Te quedan {resultado.IntentosRestantes} intentos.";
            return View("Index", await ModeloAsync(token, mensaje, cancelacion));
        }
        catch (ReglaDeNegocioException error)
        {
            return View("Index", new TecnicoPublicoModelo { Token = token, Error = error.Message });
        }
    }

    [HttpPost("cerrar")]
    [RequestSizeLimit(TiposArchivo.TamanoEnvio)]
    [EnableRateLimiting(CuentaController.PoliticaLimite)]
    public async Task<IActionResult> Cerrar(string token, List<IFormFile> fotos, string? comentario, CancellationToken cancelacion)
    {
        if (!TokenSeguro.TieneFormatoValido(token))
        {
            return View("Index", await ModeloAsync(token, null, cancelacion));
        }

        var hash = TokenSeguro.Hash(token);
        var enlace = await repositorio.EnlaceVigenteAsync(hash, cancelacion);
        if (enlace is null)
        {
            return View("Index", await ModeloAsync(token, "Primero ingresa el PIN.", cancelacion));
        }

        var validas = await ReportesController.ValidarFotosAsync(fotos, ModelState, cancelacion);
        if (!ModelState.IsValid)
        {
            return View("Index", await ModeloAsync(token, ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage, cancelacion));
        }

        await ReportesController.GuardarFotosAsync(validas, enlace.Value.ReporteId, null, enlace.Value.EnlaceId, "FotoTrabajo", archivos, almacen, cancelacion);
        try
        {
            await repositorio.CerrarPorTecnicoAsync(hash, string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(), cancelacion);
            return View("Index", new TecnicoPublicoModelo { Token = token, Mensaje = "¡Listo! El trabajo quedó marcado como terminado. Gracias." });
        }
        catch (ReglaDeNegocioException error)
        {
            return View("Index", await ModeloAsync(token, error.Message, cancelacion));
        }
    }

    [HttpGet("foto/{id:int}")]
    public async Task<IActionResult> Foto(string token, int id, CancellationToken cancelacion)
    {
        var modelo = await ModeloAsync(token, null, cancelacion);
        var foto = modelo.Reporte?.Fotos.FirstOrDefault(f => f.Id == id);
        if (foto is null)
        {
            return NotFound();
        }

        var tipo = foto.PublicId.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : foto.PublicId.EndsWith(".webp", StringComparison.Ordinal) ? "image/webp" : "image/jpeg";
        var flujo = await almacen.AbrirAsync(foto.PublicId, tipo, cancelacion);
        return flujo is null ? NotFound() : File(flujo, tipo);
    }

    private async Task<TecnicoPublicoModelo> ModeloAsync(string token, string? error, CancellationToken cancelacion)
    {
        if (!TokenSeguro.TieneFormatoValido(token))
        {
            return new TecnicoPublicoModelo { Token = token, Error = "Este enlace no es válido. Pide uno nuevo a la administración." };
        }

        try
        {
            return new TecnicoPublicoModelo { Token = token, Reporte = await repositorio.ParaTecnicoAsync(TokenSeguro.Hash(token), cancelacion), Error = error };
        }
        catch (ReglaDeNegocioException ex)
        {
            return new TecnicoPublicoModelo { Token = token, Error = ex.Message };
        }
    }
}
