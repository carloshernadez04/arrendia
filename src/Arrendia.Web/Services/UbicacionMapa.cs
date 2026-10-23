using System.Globalization;
using System.Text.RegularExpressions;

namespace Arrendia.Web.Services;

public sealed record Ubicacion(decimal? Latitud, decimal? Longitud, string? Error)
{
    public bool TieneCoordenadas => Latitud is not null && Longitud is not null;
}

public static partial class UbicacionMapa
{
    public const decimal LatitudMinima = 13.1m;
    public const decimal LatitudMaxima = 14.5m;
    public const decimal LongitudMinima = -90.2m;
    public const decimal LongitudMaxima = -87.6m;

    public static Ubicacion Interpretar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return new Ubicacion(null, null, null);
        }

        texto = texto.Trim();
        var coincidencia = ConArroba().Match(texto);
        if (!coincidencia.Success) { coincidencia = ConDatos().Match(texto); }
        if (!coincidencia.Success) { coincidencia = ConConsulta().Match(texto); }
        if (!coincidencia.Success) { coincidencia = SoloNumeros().Match(texto); }

        if (!coincidencia.Success
            || !decimal.TryParse(coincidencia.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitud)
            || !decimal.TryParse(coincidencia.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitud))
        {
            return new Ubicacion(null, null, EnlaceCorto().IsMatch(texto)
                ? "Los enlaces cortos de Google Maps no se pueden leer. Ábrelo y copia las coordenadas o el enlace largo."
                : "No pudimos leer la ubicación. Pega las coordenadas (13.7189, -89.7242) o el enlace largo de Google Maps.");
        }

        if (latitud is < LatitudMinima or > LatitudMaxima || longitud is < LongitudMinima or > LongitudMaxima)
        {
            return new Ubicacion(null, null, "Esas coordenadas quedan fuera de El Salvador. Revisa que no estén invertidas: primero la latitud y luego la longitud.");
        }

        return new Ubicacion(Math.Round(latitud, 6), Math.Round(longitud, 6), null);
    }

    public static string Texto(decimal? latitud, decimal? longitud) =>
        latitud is null || longitud is null
            ? ""
            : string.Create(CultureInfo.InvariantCulture, $"{latitud:0.######}, {longitud:0.######}");

    public static string EnlaceMaps(string nombre, string municipio, decimal? latitud, decimal? longitud) =>
        latitud is null || longitud is null
            ? $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString($"{nombre} {municipio} El Salvador")}"
            : string.Create(CultureInfo.InvariantCulture, $"https://www.google.com/maps/search/?api=1&query={latitud},{longitud}");

    [GeneratedRegex(@"@(-?\d+\.\d+),(-?\d+\.\d+)")]
    private static partial Regex ConArroba();

    [GeneratedRegex(@"!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)")]
    private static partial Regex ConDatos();

    [GeneratedRegex(@"[?&](?:q|ll|query)=(-?\d+\.\d+)(?:,|%2C)\s*(-?\d+\.\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ConConsulta();

    [GeneratedRegex(@"^\s*(-?\d{1,3}\.\d+)\s*[,;\s]\s*(-?\d{1,3}\.\d+)\s*$")]
    private static partial Regex SoloNumeros();

    [GeneratedRegex(@"maps\.app\.goo\.gl|goo\.gl/maps", RegexOptions.IgnoreCase)]
    private static partial Regex EnlaceCorto();
}
