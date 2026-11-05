using Arrendia.Web.Services;

namespace Arrendia.Tests;

public class ReglasInquilinoTests
{
    [Theory]
    [InlineData("04567891-2", "04567891-2", true)]
    [InlineData("045678912", "04567891-2", true)]
    [InlineData("04567891-3", "04567891-2", false)]
    [InlineData("0614-010190-101-1", "04567891-2", false)]
    [InlineData("04567891-2", null, false)]
    public void ReconoceElDuiEscritoComoNit(string nit, string? dui, bool esperado) =>
        Assert.Equal(esperado, ReglasInquilino.EsElMismoDui(nit, dui));

    [Theory]
    [InlineData("06140101901011", "0614-010190-101-1")]
    [InlineData("0614-010190-101-1", "0614-010190-101-1")]
    public void NormalizaElNitDe14Digitos(string nit, string esperado)
    {
        var normalizado = ReglasInquilino.NormalizarNit(nit);
        Assert.Equal(esperado, normalizado);
        Assert.True(ReglasInquilino.NitValido(normalizado));
    }

    [Fact]
    public void ElDuiSoloNoPasaComoNitDe14Digitos() =>
        Assert.False(ReglasInquilino.NitValido(ReglasInquilino.NormalizarNit("04567891-2")));
}
