using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Arrendia.Tests;

public class SaludTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/cuenta/entrar")]
    [InlineData("/cuenta/recuperar")]
    public async Task LasPaginasPublicasResponden(string ruta)
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri(ruta, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnaRutaInexistenteDevuelve404()
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri("/no-existe", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
