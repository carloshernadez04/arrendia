using System.Globalization;

namespace Arrendia.Web.Models;

public static class Formato
{
    private static readonly CultureInfo Dolares = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-SV");
    private static readonly TimeZoneInfo ZonaSv = TimeZoneInfo.FindSystemTimeZoneById("America/El_Salvador");

    public static string Dinero(decimal? monto) => monto is null ? "—" : monto.Value.ToString("C2", Dolares);

    public static string Area(decimal? metros) => metros is null ? "—" : $"{metros.Value.ToString("0.##", Dolares)} m²";

    public static string Fecha(DateTime? fecha) => fecha is null ? "—" : fecha.Value.ToString("d MMM yyyy", Espanol);

    public static string FechaLarga(DateTime fecha) => fecha.ToString("d 'de' MMMM 'de' yyyy", Espanol);

    public static DateTime HoraSv(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ZonaSv);

    public static DateTime HoySv() => HoraSv(DateTime.UtcNow).Date;

    public static string FechaHora(DateTime? utc) => utc is null ? "—" : HoraSv(utc.Value).ToString("d MMM yyyy, h:mm tt", Espanol);

    public static string Periodo(string? periodo)
    {
        if (periodo is null || !DateTime.TryParseExact(periodo + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
        {
            return periodo ?? "—";
        }

        var texto = f.ToString("MMMM yyyy", Espanol);
        return char.ToUpper(texto[0], Espanol) + texto[1..];
    }

    public static string Porcentaje(decimal valor) => $"{valor.ToString("0.####", Dolares)} %";

    public static string Numero(decimal valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);
}

public static class Estados
{
    public static string Clase(string? estado) => estado switch
    {
        "Activo" or "Aplicado" or "Pagado" or "Disponible" or "Enviado" or "Exitoso" or "Terminado" or "Con acceso" or "Pagada" => "ok",
        "Borrador" or "PendienteFirma" or "Parcial" or "Pendiente" or "Asignado" or "Mantenimiento" or "Invitación enviada" or "Por vencer" or "Creada" => "warn",
        "Vencido" or "Anulado" or "Fallido" or "Cancelado" or "Invitación vencida" or "Fallida" or "Urgente" => "bad",
        "Ocupada" or "Abierto" or "Alta" => "info",
        _ => "",
    };

    public static string Texto(string? estado) => estado switch
    {
        "PendienteFirma" => "Pendiente de firma",
        "Mantenimiento" => "En mantenimiento",
        "Condonacion" => "Condonación",
        "Anulacion" => "Anulación",
        "Automatico" => "Automático",
        "Reparacion" => "Reparación",
        "Si" => "Sí",
        "SegunUnidad" => "Según la unidad",
        "PorcentajeCargo" => "Porcentaje del cargo",
        "AvisoCobro" => "Aviso de cobro",
        "EstadoCuenta" => "Estado de cuenta",
        "CambioCorreo" => "Cambio de correo",
        "EnlaceTecnico" => "Enlace del técnico",
        "Invitacion" => "Invitación",
        "Recuperacion" => "Recuperación",
        null => "—",
        _ => estado,
    };
}
