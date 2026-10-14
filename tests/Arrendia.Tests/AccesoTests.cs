using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Arrendia.Tests;

public partial class AccesoTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient ClienteSinRedirecciones() =>
        fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/tablero")]
    [InlineData("/portal")]
    public async Task SinSesionLasPaginasPrivadasMandanAEntrar(string ruta)
    {
        using var cliente = ClienteSinRedirecciones();

        var respuesta = await cliente.GetAsync(new Uri(ruta, UriKind.Relative));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.StartsWith("/cuenta/entrar", respuesta.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        Assert.Contains("volver=", respuesta.Headers.Location?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaRaizSinSesionMandaAEntrar()
    {
        using var cliente = ClienteSinRedirecciones();

        var respuesta = await cliente.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal("/cuenta/entrar", respuesta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnFormularioSinTokenAntiforgeryEsRechazado()
    {
        using var cliente = ClienteSinRedirecciones();
        using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "alguien@correo.sv",
            ["Contrasena"] = "contrasena123",
        });

        var respuesta = await cliente.PostAsync(new Uri("/cuenta/entrar", UriKind.Relative), contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElFormularioDeEntradaLlevaTokenAntiforgery()
    {
        using var cliente = fabrica.CreateClient();

        var html = await cliente.GetStringAsync(new Uri("/cuenta/entrar", UriKind.Relative));

        Assert.Matches(TokenAntiforgery(), html);
    }

    [Fact]
    public async Task UnEnlaceConFormatoInvalidoMuestraQueNoSirve()
    {
        using var cliente = fabrica.CreateClient();

        var html = await cliente.GetStringAsync(new Uri("/cuenta/activar/no-es-un-token", UriKind.Relative));

        Assert.Contains("Este enlace ya no sirve", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaSesionUsaUnaCookieProtegida()
    {
        using var cliente = ClienteSinRedirecciones();

        var respuesta = await cliente.GetAsync(new Uri("/cuenta/entrar", UriKind.Relative));
        var cookies = respuesta.Headers.TryGetValues("Set-Cookie", out var valores) ? string.Join(";", valores) : "";

        Assert.Contains("arrendia.af", cookies, StringComparison.Ordinal);
        Assert.Contains("httponly", cookies, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"")]
    private static partial Regex TokenAntiforgery();
}
