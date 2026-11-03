using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Administrador)]
[Route("usuarios")]
public sealed class UsuariosController(
    IRepositorioUsuarios usuarios,
    IRepositorioPropiedades propiedades,
    ServicioInvitaciones invitaciones) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancelacion) => View(await usuarios.PersonalAsync(cancelacion));

    [HttpGet("nuevo")]
    public async Task<IActionResult> Nuevo(CancellationToken cancelacion) =>
        View(new InvitacionFormulario { Opciones = await propiedades.ListarAsync(User.Id(), cancelacion) });

    [HttpPost("nuevo")]
    public async Task<IActionResult> Nuevo(InvitacionFormulario modelo, CancellationToken cancelacion)
    {
        modelo.Opciones = await propiedades.ListarAsync(User.Id(), cancelacion);
        var email = modelo.Email?.Trim().ToLowerInvariant();
        if (!ReglasInquilino.EmailValido(email))
        {
            ModelState.AddModelError(nameof(modelo.Email), "Escribe un correo válido.");
        }

        if (modelo.Rol is not (Roles.Administrador or Roles.Supervisor))
        {
            ModelState.AddModelError(nameof(modelo.Rol), "Elige el rol.");
        }

        if (modelo.Rol == Roles.Supervisor && modelo.Propiedades.Count == 0)
        {
            ModelState.AddModelError(nameof(modelo.Propiedades), "Elige al menos una propiedad para el supervisor.");
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        try
        {
            var resultado = await invitaciones.InvitarAsync(modelo.Nombre!.Trim(), email!, modelo.Rol,
                null, modelo.Rol == Roles.Supervisor ? modelo.Propiedades : null, User.Id(), cancelacion);
            Avisar(resultado.EnlaceParaCopiar, email!);
            return modelo.Rol == Roles.Supervisor ? RedirectToAction(nameof(Detalle), new { id = resultado.UsuarioId }) : RedirectToAction(nameof(Index));
        }
        catch (ReglaDeNegocioException error)
        {
            ModelState.AddModelError(nameof(modelo.Email), error.Message);
            return View(modelo);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detalle(int id, CancellationToken cancelacion)
    {
        var usuario = await usuarios.ObtenerAsync(id, cancelacion);
        if (usuario is null || usuario.Rol is not (Roles.Administrador or Roles.Supervisor))
        {
            return NotFound();
        }

        return View(new UsuarioDetalleModelo
        {
            Usuario = usuario,
            Opciones = await propiedades.ListarAsync(User.Id(), cancelacion),
            Asignadas = await usuarios.PropiedadesDeAsync(id, cancelacion),
            Permisos = await usuarios.PermisosDeAsync(id, cancelacion),
            EsUnoMismo = id == User.Id(),
        });
    }

    [HttpPost("{id:int}/permisos")]
    public async Task<IActionResult> Permisos(int id, List<int> propiedadesAsignadas, PermisosSupervisor permisos, CancellationToken cancelacion)
    {
        var usuario = await usuarios.ObtenerAsync(id, cancelacion);
        if (usuario?.Rol != Roles.Supervisor)
        {
            return NotFound();
        }

        if (propiedadesAsignadas.Count == 0)
        {
            this.Fallo("El supervisor necesita al menos una propiedad.");
            return RedirectToAction(nameof(Detalle), new { id });
        }

        await usuarios.GuardarSupervisorAsync(id, propiedadesAsignadas, permisos, User.Id(), cancelacion);
        this.Exito("Permisos guardados. Se aplican en su próxima acción.");
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/reenviar")]
    public async Task<IActionResult> Reenviar(int id, CancellationToken cancelacion)
    {
        try
        {
            var usuario = await usuarios.ObtenerAsync(id, cancelacion);
            Avisar(await invitaciones.ReenviarAsync(id, User.Id(), cancelacion), usuario?.Email ?? "");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/revocar")]
    public async Task<IActionResult> Revocar(int id, CancellationToken cancelacion)
    {
        try
        {
            await usuarios.RevocarAsync(id, User.Id(), cancelacion);
            this.Exito("Invitación revocada.");
        }
        catch (ReglaDeNegocioException error)
        {
            this.Fallo(error.Message);
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost("{id:int}/activo")]
    public async Task<IActionResult> Activo(int id, bool activo, CancellationToken cancelacion)
    {
        var usuario = await usuarios.ObtenerAsync(id, cancelacion);
        if (usuario is null || usuario.Rol is not (Roles.Administrador or Roles.Supervisor))
        {
            return NotFound();
        }

        if (!activo && id == User.Id())
        {
            this.Fallo("No puedes desactivar tu propia cuenta.");
        }
        else if (!activo && usuario.Rol == Roles.Administrador && await usuarios.AdministradoresActivosAsync(cancelacion) <= 1)
        {
            this.Fallo("Debe quedar al menos un administrador activo.");
        }
        else
        {
            await usuarios.CambiarActivoAsync(id, activo, User.Id(), cancelacion);
            this.Exito(activo ? "Cuenta reactivada." : "Cuenta desactivada. Se cerraron sus sesiones.");
        }

        return RedirectToAction(nameof(Detalle), new { id });
    }

    private void Avisar(string? enlace, string email)
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
}
