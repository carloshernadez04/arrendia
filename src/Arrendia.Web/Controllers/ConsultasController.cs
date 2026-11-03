using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Models;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arrendia.Web.Controllers;

[Authorize(Roles = Roles.Personal)]
[Route("tablero")]
public sealed class TableroController(IRepositorioConsultas consultas, IRepositorioCobros cobros, IRepositorioMantenimiento mantenimiento) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancelacion)
    {
        var (indicadores, serie) = await consultas.TableroAsync(User.Id(), cancelacion);
        return View(new TableroModelo
        {
            Indicadores = indicadores,
            Serie = serie,
            PorVencer = await consultas.PorVencerAsync(User.Id(), cancelacion),
            Morosos = [.. (await cobros.CobranzaAsync(User.Id(), null, 1, cancelacion)).Take(6)],
            Reportes = [.. (await mantenimiento.ReportesAsync(User.Id(), new FiltroReportes("Pendientes"), cancelacion)).Take(6)],
        });
    }
}

[Authorize(Roles = Roles.Personal)]
[Route("informes")]
public sealed class InformesController(IRepositorioConsultas consultas, IRepositorioPropiedades propiedades) : Controller
{
    public static readonly (string Clave, string Titulo)[] Tipos =
    [
        ("ingresos", "Ingresos"),
        ("morosidad", "Morosidad"),
        ("ocupacion", "Ocupación"),
        ("trabajos", "Trabajos por técnico"),
    ];

    [HttpGet("")]
    public async Task<IActionResult> Index(string? tipo, DateTime? desde, DateTime? hasta, int? propiedad, string? formato, CancellationToken cancelacion)
    {
        tipo = Tipos.Any(t => t.Clave == tipo) ? tipo! : "ingresos";
        var hoy = Formato.HoySv();
        var d = (desde ?? new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-5)).Date;
        var h = (hasta ?? hoy).Date;
        if (d > h)
        {
            (d, h) = (h, d);
        }

        var nombrePropiedad = propiedad is null ? "todas las propiedades"
            : (await propiedades.ObtenerAsync(propiedad.Value, User.Id(), cancelacion))?.Nombre ?? "todas las propiedades";
        IReadOnlyList<ConceptoTotal> conceptos = [];
        Informe informe;
        switch (tipo)
        {
            case "ingresos":
                var (filas, porConcepto) = await consultas.IngresosAsync(d, h, propiedad, User.Id(), cancelacion);
                conceptos = porConcepto;
                informe = new Informe
                {
                    Titulo = "Informe de ingresos",
                    Subtitulo = $"Pagos aplicados del {Formato.Fecha(d)} al {Formato.Fecha(h)} · {nombrePropiedad}",
                    Columnas = [new("Mes"), new("Propiedad"), new("Método"), new("Pagos", Numero: true), new("Total", Dinero: true)],
                    Filas = [.. filas.Select(f => new object?[] { Formato.Periodo(f.Mes), f.Propiedad, f.Metodo, f.Pagos, f.Total })],
                    Totales = ["Total", null, null, filas.Sum(f => f.Pagos), filas.Sum(f => f.Total)],
                };
                break;
            case "morosidad":
                var morosos = await consultas.MorosidadAsync(h, propiedad, User.Id(), cancelacion);
                informe = new Informe
                {
                    Titulo = "Informe de morosidad",
                    Subtitulo = $"Saldos vencidos al {Formato.Fecha(h)} · {nombrePropiedad}",
                    Columnas = [new("Inquilino"), new("Contrato"), new("Unidad"), new("1 a 30", true), new("31 a 60", true), new("61 a 90", true),
                        new("Más de 90", true), new("Total vencido", true), new("Días", Numero: true)],
                    Filas = [.. morosos.Select(m => new object?[] { m.Inquilino, m.Contrato, $"{m.Unidad} · {m.Propiedad}", m.De1A30, m.De31A60, m.De61A90, m.MasDe90, m.TotalVencido, m.MaxDiasAtraso })],
                    Totales = ["Total", null, null, morosos.Sum(m => m.De1A30), morosos.Sum(m => m.De31A60), morosos.Sum(m => m.De61A90),
                        morosos.Sum(m => m.MasDe90), morosos.Sum(m => m.TotalVencido), null],
                };
                break;
            case "ocupacion":
                var ocupacion = await consultas.OcupacionAsync(User.Id(), cancelacion);
                informe = new Informe
                {
                    Titulo = "Informe de ocupación",
                    Subtitulo = $"Situación al {Formato.Fecha(hoy)}",
                    Columnas = [new("Propiedad"), new("Unidades", Numero: true), new("Ocupadas", Numero: true), new("Disponibles", Numero: true),
                        new("En mantenimiento", Numero: true), new("Ocupación %", Numero: true)],
                    Filas = [.. ocupacion.Select(o => new object?[] { o.Propiedad, o.Unidades, o.Ocupadas, o.Disponibles, o.EnMantenimiento, o.PorcentajeOcupacion ?? 0m })],
                    Totales = ["Total", ocupacion.Sum(o => o.Unidades), ocupacion.Sum(o => o.Ocupadas), ocupacion.Sum(o => o.Disponibles), ocupacion.Sum(o => o.EnMantenimiento), null],
                };
                break;
            default:
                var trabajos = await consultas.TrabajosAsync(d, h, User.Id(), cancelacion);
                informe = new Informe
                {
                    Titulo = "Trabajos por técnico",
                    Subtitulo = $"Reportes asignados del {Formato.Fecha(d)} al {Formato.Fecha(h)}",
                    Columnas = [new("Técnico"), new("Asignados", Numero: true), new("Terminados", Numero: true), new("Costo total", true), new("Horas promedio", Numero: true)],
                    Filas = [.. trabajos.Select(t => new object?[] { t.Tecnico, t.Reportes, t.Terminados, t.CostoTotal, t.HorasPromedio })],
                    Totales = ["Total", trabajos.Sum(t => t.Reportes), trabajos.Sum(t => t.Terminados), trabajos.Sum(t => t.CostoTotal), null],
                };
                break;
        }

        var archivo = $"{tipo}-{hoy:yyyyMMdd}";
        return formato switch
        {
            "pdf" => this.Pdf(Exportacion.Pdf(informe), $"{archivo}.pdf"),
            "xlsx" => File(Exportacion.Excel(informe), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{archivo}.xlsx"),
            _ => View(new InformesModelo
            {
                Tipo = tipo,
                Informe = informe,
                Conceptos = conceptos,
                Desde = d,
                Hasta = h,
                PropiedadId = propiedad,
                Propiedades = await propiedades.ListarAsync(User.Id(), cancelacion),
            }),
        };
    }
}

[Authorize(Roles = Roles.Administrador)]
[Route("bitacora")]
public sealed class BitacoraController(IRepositorioConsultas consultas) : Controller
{
    private const int Tamano = 50;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? tabla, string? accion, int? usuario, DateTime? desde, DateTime? hasta, int pagina, CancellationToken cancelacion)
    {
        var filtro = new FiltroBitacora(string.IsNullOrWhiteSpace(tabla) ? null : tabla, string.IsNullOrWhiteSpace(accion) ? null : accion.Trim(),
            usuario, desde, hasta, Math.Max(pagina, 1));
        var (filas, total) = await consultas.BitacoraAsync(filtro, Tamano, cancelacion);
        return View(new BitacoraModelo
        {
            Filas = filas,
            Filtro = filtro,
            Paginacion = new Paginacion { Pagina = filtro.Pagina, Tamano = Tamano, Total = total },
            Tablas = await consultas.TablasBitacoraAsync(cancelacion),
            Usuarios = await consultas.UsuariosBitacoraAsync(cancelacion),
            Ejecuciones = await consultas.EjecucionesAsync(cancelacion),
        });
    }
}

[Authorize(Roles = Roles.Administrador)]
[Route("correos")]
public sealed class CorreosController(IRepositorioCorreos correos, ICorreo servidor) : Controller
{
    public static readonly string[] Tipos = ["AvisoCobro", "Comprobante", "Invitacion", "Recuperacion", "EnlaceTecnico", "CambioCorreo"];
    private const int Tamano = 50;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? estado, string? tipo, string? buscar, int pagina, CancellationToken cancelacion)
    {
        var filtro = new FiltroCorreos(estado is "Pendiente" or "Enviado" or "Fallido" ? estado : null, Tipos.Contains(tipo) ? tipo : null, buscar, Math.Max(pagina, 1));
        var (filas, total) = await correos.ListarAsync(filtro, Tamano, cancelacion);
        return View(new CorreosModelo
        {
            Filas = filas,
            Filtro = filtro,
            Paginacion = new Paginacion { Pagina = filtro.Pagina, Tamano = Tamano, Total = total },
            ServidorListo = servidor.PuedeEnviar,
        });
    }

    [HttpPost("{id:long}/reintentar")]
    public async Task<IActionResult> Reintentar(long id, CancellationToken cancelacion)
    {
        if (await correos.ReintentarAsync(id, cancelacion))
        {
            this.Exito("El correo volvió a la cola: se enviará en el próximo minuto.");
        }
        else
        {
            this.Fallo("Solo se reintentan avisos de cobro y comprobantes que fallaron. Los enlaces se generan de nuevo desde su pantalla.");
        }

        return RedirectToAction(nameof(Index));
    }
}
