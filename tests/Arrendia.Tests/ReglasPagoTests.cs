using Arrendia.Web.Services;

namespace Arrendia.Tests;

public class ReglasPagoTests
{
    private static readonly DateTime Enero = new(2026, 1, 5);
    private static readonly DateTime Febrero = new(2026, 2, 5);
    private static readonly int[] OrdenEsperado = [2, 1, 3];

    private static List<CargoPendiente> Cargos() =>
    [
        new(3, "Renta de febrero", 500m, Febrero, false),
        new(1, "Renta de enero", 500m, Enero, false),
        new(2, "Mora de enero", 12.50m, Enero, true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-10)]
    public void ElMontoDebeSerMayorACero(double? monto)
    {
        var error = ReglasPago.ValidarMonto(monto is null ? null : (decimal)monto, 500m);

        Assert.Equal("El monto debe ser mayor a $0.00.", error);
    }

    [Fact]
    public void NoSePuedePagarMasDeLoQueSeDebe()
    {
        var error = ReglasPago.ValidarMonto(1012.51m, 1012.50m);

        Assert.NotNull(error);
        Assert.Contains("no hay saldo a favor", error, StringComparison.Ordinal);
    }

    [Fact]
    public void SePuedePagarExactamenteLaDeuda()
    {
        Assert.Null(ReglasPago.ValidarMonto(1012.50m, 1012.50m));
    }

    [Fact]
    public void NoSeAceptanMasDeDosDecimales()
    {
        Assert.Equal("El monto solo puede tener dos decimales.", ReglasPago.ValidarMonto(10.001m, 500m));
    }

    [Fact]
    public void SinDeudaNoHayNadaQuePagar()
    {
        Assert.Equal("Este contrato no tiene saldo pendiente.", ReglasPago.ValidarMonto(10m, 0m));
    }

    [Fact]
    public void SePagaPrimeroLoMasAntiguoYLaMoraAntesQueSuCargo()
    {
        var aplicaciones = ReglasPago.Repartir(Cargos(), 1012.50m);

        Assert.Equal(OrdenEsperado, aplicaciones.Select(a => a.CargoId));
        Assert.All(aplicaciones, a => Assert.Equal(a.Saldo, a.Aplicado));
    }

    [Fact]
    public void UnPagoParcialDejaElRestoPendiente()
    {
        var aplicaciones = ReglasPago.Repartir(Cargos(), 300m);

        Assert.Equal(12.50m, aplicaciones[0].Aplicado);
        Assert.Equal(287.50m, aplicaciones[1].Aplicado);
        Assert.True(aplicaciones[1].EsParcial);
        Assert.Equal(0m, aplicaciones[2].Aplicado);
        Assert.Equal(300m, aplicaciones.Sum(a => a.Aplicado));
    }

    [Fact]
    public void SinPagosParcialesElMontoDebeCubrirCargosCompletos()
    {
        Assert.NotNull(ReglasPago.Validar(300m, Cargos(), permiteParciales: false));
        Assert.Null(ReglasPago.Validar(512.50m, Cargos(), permiteParciales: false));
        Assert.Null(ReglasPago.Validar(300m, Cargos(), permiteParciales: true));
    }

    [Fact]
    public void LaDeudaSumaSoloLosSaldosPendientes()
    {
        var cargos = Cargos();
        cargos.Add(new CargoPendiente(4, "Pagado", 0m, Enero, false));

        Assert.Equal(1012.50m, ReglasPago.Deuda(cargos));
    }
}
