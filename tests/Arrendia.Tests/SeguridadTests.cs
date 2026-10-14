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
    [InlineData("Cross-Origin-Embedder-Policy")]
    [InlineData("Cache-Control")]
    public async Task LasRespuestasLlevanEncabezadosDeSeguridad(string encabezado)
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri("/", UriKind.Relative));

        Assert.True(respuesta.Headers.Contains(encabezado), $"Falta el encabezado {encabezado}");
    }

    [Theory]
    [InlineData("/cuenta/entrar")]
    [InlineData("/cuenta/recuperar")]
    [InlineData("/robots.txt")]
    public async Task LasPaginasConFormularioNoSeGuardanEnCache(string ruta)
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri(ruta, UriKind.Relative));

        var cache = respuesta.Headers.CacheControl;
        Assert.True(cache is { NoStore: true, NoCache: true, MustRevalidate: true }, cache?.ToString());
    }

    [Fact]
    public async Task NoRevelaElServidorWeb()
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.False(respuesta.Headers.Contains("Server"));
    }
}
