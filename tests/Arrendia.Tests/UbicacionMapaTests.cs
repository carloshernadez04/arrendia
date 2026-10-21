using Arrendia.Web.Services;

namespace Arrendia.Tests;

public class UbicacionMapaTests
{
    [Theory]
    [InlineData("13.7189, -89.7242", 13.7189, -89.7242)]
    [InlineData("  13.7189,-89.7242  ", 13.7189, -89.7242)]
    [InlineData("13.7189 -89.7242", 13.7189, -89.7242)]
    [InlineData("https://www.google.com/maps/place/Plaza/@13.718912,-89.724217,17z/data=x", 13.718912, -89.724217)]
    [InlineData("https://www.google.com/maps?q=13.7396,-89.6694", 13.7396, -89.6694)]
    [InlineData("https://www.google.com/maps/place/X/data=!3d13.71!4d-89.72", 13.71, -89.72)]
    [InlineData("13.71891234, -89.72421789", 13.718912, -89.724218)]
    public void LeeCoordenadasYEnlacesLargos(string texto, double latitud, double longitud)
    {
        var ubicacion = UbicacionMapa.Interpretar(texto);

        Assert.Null(ubicacion.Error);
        Assert.Equal((decimal)latitud, ubicacion.Latitud);
        Assert.Equal((decimal)longitud, ubicacion.Longitud);
    }

    [Theory]
    [InlineData("-89.7242, 13.7189", "invertidas")]
    [InlineData("19.4326, -99.1332", "fuera de El Salvador")]
    [InlineData("https://maps.app.goo.gl/abc123", "enlaces cortos")]
    [InlineData("frente al parque", "No pudimos leer")]
    public void RechazaUbicacionesInvalidas(string texto, string mensaje)
    {
        var ubicacion = UbicacionMapa.Interpretar(texto);

        Assert.NotNull(ubicacion.Error);
        Assert.Contains(mensaje, ubicacion.Error, StringComparison.Ordinal);
        Assert.False(ubicacion.TieneCoordenadas);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinTextoNoHayUbicacionNiError(string? texto)
    {
        var ubicacion = UbicacionMapa.Interpretar(texto);

        Assert.Null(ubicacion.Error);
        Assert.False(ubicacion.TieneCoordenadas);
    }

    [Fact]
    public void ElEnlaceDeMapsUsaCoordenadasSiLasHay()
    {
        Assert.Equal("https://www.google.com/maps/search/?api=1&query=13.7189,-89.7242",
            UbicacionMapa.EnlaceMaps("Plaza", "Sonsonate", 13.7189m, -89.7242m));
        Assert.Equal("https://www.google.com/maps/search/?api=1&query=Plaza%20Sonsonate%20El%20Salvador",
            UbicacionMapa.EnlaceMaps("Plaza", "Sonsonate", null, null));
    }

    [Fact]
    public void ElTextoDeLaUbicacionSeVuelveALeerIgual()
    {
        var texto = UbicacionMapa.Texto(13.718912m, -89.724217m);

        Assert.Equal("13.718912, -89.724217", texto);
        Assert.Equal(13.718912m, UbicacionMapa.Interpretar(texto).Latitud);
    }
}
