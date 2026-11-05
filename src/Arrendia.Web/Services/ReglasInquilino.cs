using System.Text.RegularExpressions;

namespace Arrendia.Web.Services;

public static partial class ReglasInquilino
{
    public static bool DuiValido(string? dui)
    {
        if (dui is null || !FormatoDui().IsMatch(dui))
        {
            return false;
        }

        var suma = 0;
        for (var i = 0; i < 8; i++)
        {
            suma += (dui[i] - '0') * (9 - i);
        }

        var verificador = (10 - suma % 10) % 10;
        return dui[9] - '0' == verificador;
    }

    public static bool NitValido(string? nit) => nit is not null && FormatoNit().IsMatch(nit);

    public static bool EsElMismoDui(string? nit, string? dui)
    {
        var digitos = SoloDigitos(nit);
        return digitos.Length == 9 && digitos == SoloDigitos(dui);
    }

    public static string? NormalizarDui(string? dui)
    {
        var digitos = SoloDigitos(dui);
        return digitos.Length == 9 ? $"{digitos[..8]}-{digitos[8]}" : dui?.Trim();
    }

    public static string? NormalizarNit(string? nit)
    {
        var digitos = SoloDigitos(nit);
        return digitos.Length == 14 ? $"{digitos[..4]}-{digitos[4..10]}-{digitos[10..13]}-{digitos[13]}" : nit?.Trim();
    }

    public static string? NormalizarTelefono(string? telefono)
    {
        var digitos = SoloDigitos(telefono);
        if (digitos.Length == 11 && digitos.StartsWith("503", StringComparison.Ordinal))
        {
            digitos = digitos[3..];
        }

        return digitos.Length == 8 ? $"{digitos[..4]}-{digitos[4..]}" : null;
    }

    public static bool EmailValido(string? email) => email is not null && FormatoEmail().IsMatch(email.Trim());

    private static string SoloDigitos(string? texto) => new((texto ?? "").Where(char.IsAsciiDigit).ToArray());

    [GeneratedRegex(@"^\d{8}-\d$")]
    private static partial Regex FormatoDui();

    [GeneratedRegex(@"^\d{4}-\d{6}-\d{3}-\d$")]
    private static partial Regex FormatoNit();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex FormatoEmail();
}
