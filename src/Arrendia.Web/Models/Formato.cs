using System.Globalization;

namespace Arrendia.Web.Models;

public static class Formato
{
    private static readonly CultureInfo Dolares = CultureInfo.GetCultureInfo("en-US");

    public static string Dinero(decimal? monto) => monto is null ? "—" : monto.Value.ToString("C2", Dolares);

    public static string Area(decimal? metros) => metros is null ? "—" : $"{metros.Value.ToString("0.##", Dolares)} m²";
}
