using Arrendia.Web.Services;

namespace Arrendia.Tests;

public class WompiTests
{
    private const string Secreto = "secreto-de-prueba";
    private const string Aviso = """
        {"IdTransaccion":"8b3f-001","ResultadoTransaccion":"ExitosaAprobada","Monto":512.50,"FechaTransaccion":"2026-10-31T15:20:00Z",
         "FormaPagoUtilizada":"Tarjeta","EsProductiva":false,"EnlacePago":{"Id":77,"IdentificadorEnlaceComercio":"ARR123"}}
        """;

    [Fact]
    public void UnAvisoFirmadoConElSecretoEsValido()
    {
        var firma = FirmaWompi.Calcular(Aviso, Secreto);

        Assert.True(FirmaWompi.Verificar(Aviso, firma, Secreto));
        Assert.True(FirmaWompi.Verificar(Aviso, firma.ToUpperInvariant(), Secreto));
    }

    [Fact]
    public void UnAvisoAlteradoNoEsValido()
    {
        var firma = FirmaWompi.Calcular(Aviso, Secreto);
        var alterado = Aviso.Replace("512.50", "5.00", StringComparison.Ordinal);

        Assert.False(FirmaWompi.Verificar(alterado, firma, Secreto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void SinFirmaCorrectaNoEsValido(string? firma)
    {
        Assert.False(FirmaWompi.Verificar(Aviso, firma, Secreto));
    }

    [Fact]
    public void SinSecretoConfiguradoNadaEsValido()
    {
        Assert.False(FirmaWompi.Verificar(Aviso, FirmaWompi.Calcular(Aviso, Secreto), null));
    }

    [Fact]
    public void LeeLosDatosDelAviso()
    {
        var aviso = FirmaWompi.Leer(Aviso);

        Assert.NotNull(aviso);
        Assert.Equal("8b3f-001", aviso.IdTransaccion);
        Assert.Equal("ARR123", aviso.Referencia);
        Assert.True(aviso.Aprobado);
        Assert.Equal(512.50m, aviso.Monto);
        Assert.False(aviso.EsProductivo);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("{}")]
    [InlineData("""{"IdTransaccion":"1","Monto":10}""")]
    public void UnAvisoIncompletoNoSeLee(string cuerpo)
    {
        Assert.Null(FirmaWompi.Leer(cuerpo));
    }

    [Fact]
    public void UnPagoDeclinadoNoQuedaAprobado()
    {
        var aviso = FirmaWompi.Leer(Aviso.Replace("ExitosaAprobada", "Declinada", StringComparison.Ordinal));

        Assert.False(aviso!.Aprobado);
    }
}
