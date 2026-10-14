using Arrendia.Web.Services;

namespace Arrendia.Tests;

public class ReglasCuentaTests
{
    [Theory]
    [InlineData(null, null, "Escribe una contraseña.")]
    [InlineData("corta1", "corta1", "al menos 10")]
    [InlineData("solamenteletras", "solamenteletras", "letras y números")]
    [InlineData("1234567890", "1234567890", "letras y números")]
    [InlineData("clave-segura-1", "clave-segura-2", "no coinciden")]
    public void RechazaContrasenasDebiles(string? contrasena, string? confirmacion, string mensaje)
    {
        var error = ReglasContrasena.Validar(contrasena, confirmacion);

        Assert.NotNull(error);
        Assert.Contains(mensaje, error, StringComparison.Ordinal);
    }

    [Fact]
    public void AceptaUnaContrasenaValida() => Assert.Null(ReglasContrasena.Validar("clave-segura-2026", "clave-segura-2026"));

    [Fact]
    public void NormalizaElCorreo() => Assert.Equal("ana@correo.sv", ReglasContrasena.NormalizarEmail("  Ana@Correo.SV "));

    [Fact]
    public void LosTokensSonUnicosYSuHashMide32Bytes()
    {
        var a = TokenSeguro.Generar();
        var b = TokenSeguro.Generar();

        Assert.NotEqual(a, b);
        Assert.True(TokenSeguro.TieneFormatoValido(a));
        Assert.Equal(32, TokenSeguro.Hash(a).Length);
        Assert.Equal(TokenSeguro.Hash(a), TokenSeguro.Hash(a));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corto")]
    [InlineData("../../etc/passwd/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void RechazaTokensConFormatoInvalido(string? token) => Assert.False(TokenSeguro.TieneFormatoValido(token));
}
