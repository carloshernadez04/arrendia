using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Arrendia.Tests;

public class SaludTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/")]
    public async Task LaAplicacionResponde(string ruta)
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync(new Uri(ruta, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
