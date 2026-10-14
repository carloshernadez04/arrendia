namespace Arrendia.Web.Services;

public static class ReglasContrasena
{
    public const int LongitudMinima = 10;
    public const int LongitudMaxima = 128;

    public static string? Validar(string? contrasena, string? confirmacion)
    {
        if (string.IsNullOrEmpty(contrasena))
        {
            return "Escribe una contraseña.";
        }

        if (contrasena.Length < LongitudMinima)
        {
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";
        }

        if (contrasena.Length > LongitudMaxima)
        {
            return $"La contraseña no puede tener más de {LongitudMaxima} caracteres.";
        }

        if (!contrasena.Any(char.IsLetter) || !contrasena.Any(char.IsDigit))
        {
            return "La contraseña debe tener letras y números.";
        }

        if (contrasena != confirmacion)
        {
            return "Las contraseñas no coinciden.";
        }

        return null;
    }

    public static string NormalizarEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();
}
