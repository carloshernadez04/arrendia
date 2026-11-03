using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Personal)]
[Route("inquilinos")]
public sealed class InquilinosController(
    IRepositorioInquilinos repositorio,
    IRepositorioPermisos permisos,
    IRepositorioUsuarios usuarios,
    ServicioInvitaciones invitaciones) : Controller
{
    public static readonly string[] Accesos = ["Sin acceso", "Invitación enviada", "Invitación vencida", "Con acceso", "Desactivado"];

    [HttpGet("")]
    public async Task<IActionResult> Index(string? buscar, string? estado, string? acceso, CancellationToken cancelacion)
    {
        var filtro = new FiltroInquilinos(buscar, estado is "Activo" or "Inactivo" ? estado : null, Accesos.Contains(acceso) ? acceso : null);
        return View(new InquilinosModelo
        {
            Inquilinos = await repositorio.ListarAsync(User.Id(), filtro, cancelacion),
            Filtro = filtro,
            Permisos = await permisos.DeAsync(User.Id(), cancelacion),
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancelacion)
    {
        var modelo = await DetalleAsync(id, cancelacion);
        return modelo is null ? NotFound() : View(modelo);
    }

    [HttpGet("nuevo")]
    public async Task<IActionResult> Nuevo(CancellationToken cancelacion) =>
        await PuedeEditarAsync(cancelacion) ? View("Formulario", new InquilinoFormulario()) : Forbid();

    [HttpPost("nuevo")]
    public async Task<IActionResult> Nuevo(InquilinoFormulario modelo, CancellationToken cancelacion)
    {
        if (!await PuedeEditarAsync(cancelacion))
        {
            return Forbid();
        }

        modelo.Id = null;
        var datos = Validar(modelo);
        var email = modelo.EmailContacto?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(email) && !ReglasInquilino.EmailValido(email))
        {
            ModelState.AddModelError(nameof(modelo.EmailContacto), "Escribe un correo válido.");
        }

        if (datos is null || !ModelState.IsValid)
        {
            return View("Formulario", modelo);
        }

        try
        {
            var contacto = string.IsNullOrEmpty(email) ? null : new DatosContacto(datos.RepresentanteLegal ?? datos.Nombre, email, datos.Telefono, true);
            var id = await repositorio.CrearAsync(datos, contacto, User.Id(), cancelacion);
            this.Exito($"Inquilino «{datos.Nombre}» registrado.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError(modelo.TipoPersona == "J" ? nameof(modelo.Nit) : nameof(modelo.Dui), error.Message);
            return View("Formulario", modelo);
        }
    }

    [HttpGet("{id:int}/editar")]
    public async Task<IActionResult> Editar(int id, CancellationToken cancelacion)
    {
        if (!await PuedeEditarAsync(cancelacion))
        {
            return Forbid();
        }

        var i = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        return i is null
            ? NotFound()
            : View("Formulario", new InquilinoFormulario
            {
                Id = i.Id,
                TipoPersona = i.TipoPersona,
                Nombre = i.Nombre,
                Dui = i.Dui,
                Nit = i.Nit,
                Nrc = i.Nrc,
                RepresentanteLegal = i.RepresentanteLegal,
                Telefono = i.Telefono,
                DireccionFiscal = i.DireccionFiscal,
            });
    }

    [HttpPost("{id:int}/editar")]
    public async Task<IActionResult> Editar(int id, InquilinoFormulario modelo, CancellationToken cancelacion)
    {
        if (!await PuedeEditarAsync(cancelacion))
        {
            return Forbid();
        }

        if (await repositorio.ObtenerAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        modelo.Id = id;
        var datos = Validar(modelo);
        if (datos is null)
        {
            return View("Formulario", modelo);
        }

        try
        {
            await repositorio.ActualizarAsync(id, datos, User.Id(), cancelacion);
            this.Exito("Cambios guardados.");
            return RedirectToAction(nameof(Detalle), new { id });
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError(modelo.TipoPersona == "J" ? nameof(modelo.Nit) : nameof(modelo.Dui), error.Message);
            return View("Formulario", modelo);
        }
    }

    [HttpPost("{id:int}/estado")]
    public async Task<IActionResult> Estado(int id, string estado, CancellationToken cancelacion)
    {
        if (!await PuedeEditarAsync(cancelacion) || estado is not ("Activo" or "Inactivo"))
        {
            return Forbid();
        }

        if (await repositorio.ObtenerAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        if (estado == "Inactivo" && await repositorio.TieneContratoActivoAsync(id, cancelacion))
        {
            this.Fallo("No se puede desactivar: tiene contratos activos o en trámite.");
        }
        else
        {
            await repositorio.CambiarEstadoAsync(id, estado, User.Id(), cancelacion);
            this.Exito(estado == "Activo" ? "Inquilino activado." : "Inquilino desactivado.");
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/contactos")]
    public async Task<IActionResult> AgregarContacto(int id, [Bind(Prefix = "NuevoContacto")] ContactoFormulario contacto, CancellationToken cancelacion)
    {
        var (salida, datos) = await ValidarContactoAsync(id, contacto, cancelacion);
        if (datos is null)
        {
            return salida!;
        }

        await repositorio.AgregarContactoAsync(id, datos, User.Id(), cancelacion);
        this.Exito("Contacto agregado.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/contactos/{contactoId:int}")]
    public async Task<IActionResult> EditarContacto(int id, int contactoId, ContactoFormulario contacto, CancellationToken cancelacion)
    {
        var (salida, datos) = await ValidarContactoAsync(id, contacto, cancelacion);
        if (datos is null)
        {
            return salida!;
        }

        await repositorio.ActualizarContactoAsync(id, contactoId, datos, User.Id(), cancelacion);
        this.Exito("Contacto actualizado.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/contactos/{contactoId:int}/principal")]
    public async Task<IActionResult> Principal(int id, int contactoId, CancellationToken cancelacion)
    {
        if (!await PuedeEditarAsync(cancelacion) || await repositorio.ObtenerAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        await repositorio.HacerPrincipalAsync(id, contactoId, User.Id(), cancelacion);
        this.Exito("Contacto principal actualizado.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/contactos/{contactoId:int}/quitar")]
    public async Task<IActionResult> QuitarContacto(int id, int contactoId, CancellationToken cancelacion)
    {
        if (!await PuedeEditarAsync(cancelacion) || await repositorio.ObtenerAsync(id, User.Id(), cancelacion) is null)
        {
            return NotFound();
        }

        await repositorio.DesactivarContactoAsync(id, contactoId, User.Id(), cancelacion);
        this.Exito("Contacto quitado. Ya no recibirá avisos.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/portal/invitar")]
    public async Task<IActionResult> InvitarPortal(int id, string? email, CancellationToken cancelacion)
    {
        var inquilino = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (inquilino is null)
        {
            return NotFound();
        }

        email = email?.Trim().ToLowerInvariant();
        if (!ReglasInquilino.EmailValido(email))
        {
            this.Fallo("Escribe un correo válido para el acceso al portal.");
            return RedirectToAction(nameof(Detalle), new { id });
        }

        try
        {
            var resultado = await invitaciones.InvitarAsync(inquilino.Nombre, email!, Roles.Inquilino, id, null, User.Id(), cancelacion);
            AvisarInvitacion(resultado.EnlaceParaCopiar, email!);
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/portal/reenviar")]
    public async Task<IActionResult> ReenviarPortal(int id, CancellationToken cancelacion)
    {
        var inquilino = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (inquilino?.UsuarioId is not { } usuarioId)
        {
            return NotFound();
        }

        try
        {
            AvisarInvitacion(await invitaciones.ReenviarAsync(usuarioId, User.Id(), cancelacion), inquilino.EmailPortal ?? "");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/portal/revocar")]
    public async Task<IActionResult> RevocarPortal(int id, CancellationToken cancelacion)
    {
        var inquilino = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (inquilino?.UsuarioId is not { } usuarioId)
        {
            return NotFound();
        }

        try
        {
            await usuarios.RevocarAsync(usuarioId, User.Id(), cancelacion);
            this.Exito("Invitación revocada.");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/portal/acceso")]
    public async Task<IActionResult> AccesoPortal(int id, bool activo, CancellationToken cancelacion)
    {
        var inquilino = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (inquilino?.UsuarioId is not { } usuarioId)
        {
            return NotFound();
        }

        if (!(await permisos.DeAsync(User.Id(), cancelacion)).ActivarContratos)
        {
            return Forbid();
        }

        await usuarios.CambiarActivoAsync(usuarioId, activo, User.Id(), cancelacion);
        this.Exito(activo ? "Acceso al portal reactivado." : "Acceso al portal desactivado. Se cerraron sus sesiones.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private void AvisarInvitacion(string? enlace, string email)
    {
        if (enlace is null)
        {
            this.Exito($"Invitación enviada a {email}.");
        }
        else
        {
            this.EnlaceParaCopiar($"No se pudo enviar el correo. Copia el enlace de activación y envíaselo a {email} (vence en 7 días):", enlace);
        }
    }

    private async Task<InquilinoDetalleModelo?> DetalleAsync(int id, CancellationToken cancelacion)
    {
        var inquilino = await repositorio.ObtenerAsync(id, User.Id(), cancelacion);
        if (inquilino is null)
        {
            return null;
        }

        return new InquilinoDetalleModelo
        {
            Inquilino = inquilino,
            Contactos = await repositorio.ContactosAsync(id, cancelacion),
            Contratos = await repositorio.ContratosAsync(id, User.Id(), cancelacion),
            Permisos = await permisos.DeAsync(User.Id(), cancelacion),
        };
    }

    private async Task<bool> PuedeEditarAsync(CancellationToken cancelacion) => (await permisos.DeAsync(User.Id(), cancelacion)).CrearContratos;

    private async Task<(IActionResult? Salida, DatosContacto? Datos)> ValidarContactoAsync(int id, ContactoFormulario contacto, CancellationToken cancelacion)
    {
        if (!await PuedeEditarAsync(cancelacion))
        {
            return (Forbid(), null);
        }

        if (await repositorio.ObtenerAsync(id, User.Id(), cancelacion) is null)
        {
            return (NotFound(), null);
        }

        var email = contacto.Email?.Trim().ToLowerInvariant();
        var telefono = string.IsNullOrWhiteSpace(contacto.Telefono) ? null : ReglasInquilino.NormalizarTelefono(contacto.Telefono);
        string? error = null;
        if (string.IsNullOrWhiteSpace(contacto.Nombre))
        {
            error = "Escribe el nombre del contacto.";
        }
        else if (!ReglasInquilino.EmailValido(email))
        {
            error = "Escribe un correo válido para el contacto.";
        }
        else if (!string.IsNullOrWhiteSpace(contacto.Telefono) && telefono is null)
        {
            error = "El teléfono debe tener 8 dígitos.";
        }

        if (error is not null)
        {
            this.Fallo(error);
            return (RedirectToAction(nameof(Detalle), new { id }), null);
        }

        return (null, new DatosContacto(contacto.Nombre!.Trim(), email!, telefono, contacto.RecibeAvisos));
    }

    private DatosInquilino? Validar(InquilinoFormulario m)
    {
        if (m.TipoPersona is not ("N" or "J"))
        {
            ModelState.AddModelError(nameof(m.TipoPersona), "Elige persona natural o jurídica.");
        }

        var dui = string.IsNullOrWhiteSpace(m.Dui) ? null : ReglasInquilino.NormalizarDui(m.Dui);
        var nit = string.IsNullOrWhiteSpace(m.Nit) ? null : ReglasInquilino.NormalizarNit(m.Nit);
        if (m.TipoPersona == "N" && dui is null)
        {
            ModelState.AddModelError(nameof(m.Dui), "Escribe el DUI.");
        }

        if (dui is not null && !ReglasInquilino.DuiValido(dui))
        {
            ModelState.AddModelError(nameof(m.Dui), "El DUI no es válido. Revisa los 9 dígitos (formato 00000000-0).");
        }

        if (m.TipoPersona == "J" && nit is null)
        {
            ModelState.AddModelError(nameof(m.Nit), "Escribe el NIT de la empresa.");
        }

        if (nit is not null && !ReglasInquilino.NitValido(nit))
        {
            ModelState.AddModelError(nameof(m.Nit), "El NIT debe tener 14 dígitos (formato 0000-000000-000-0).");
        }

        if (m.TipoPersona == "J" && string.IsNullOrWhiteSpace(m.RepresentanteLegal))
        {
            ModelState.AddModelError(nameof(m.RepresentanteLegal), "Escribe el nombre del representante legal.");
        }

        var telefono = string.IsNullOrWhiteSpace(m.Telefono) ? null : ReglasInquilino.NormalizarTelefono(m.Telefono);
        if (!string.IsNullOrWhiteSpace(m.Telefono) && telefono is null)
        {
            ModelState.AddModelError(nameof(m.Telefono), "El teléfono debe tener 8 dígitos.");
        }

        if (!ModelState.IsValid)
        {
            return null;
        }

        static string? Limpio(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
        return new DatosInquilino(m.TipoPersona, m.Nombre!.Trim(), dui, nit, Limpio(m.Nrc),
            m.TipoPersona == "J" ? Limpio(m.RepresentanteLegal) : null, telefono, Limpio(m.DireccionFiscal));
    }
}
