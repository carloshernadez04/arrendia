using Microsoft.AspNetCore.Mvc.Testing;

namespace Arrendia.Tests;

public class SeguridadTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("X-Content-Type-Options")]
    [InlineData("X-Frame-Options")]
    [InlineData("Referrer-Policy")]
    [InlineData("Permissions-Policy")]
    [InlineData("Content-Security-Policy")]
    public async Task LasRespuestasLlevanEncabezadosDeSeguridad(string encabezado)
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri("/", UriKind.Relative));

        Assert.True(respuesta.Headers.Contains(encabezado), $"Falta el encabezado {encabezado}");
    }

    [Fact]
    public async Task NoRevelaElServidorWeb()
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.False(respuesta.Headers.Contains("Server"));
    }
}
