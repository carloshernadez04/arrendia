using Arrendia.Web.Data;
using Arrendia.Web.Models;

namespace Arrendia.Tests;

public class CatalogosTests
{
    [Fact]
    public void ElSalvadorTiene14DepartamentosY262Municipios()
    {
        Assert.Equal(14, Territorio.Departamentos.Length);
        Assert.Equal(262, Territorio.Municipios.Values.Sum(m => m.Length));
    }

    [Theory]
    [InlineData("Ahuachapán", 12)]
    [InlineData("Cabañas", 9)]
    [InlineData("Chalatenango", 33)]
    [InlineData("Cuscatlán", 16)]
    [InlineData("La Libertad", 22)]
    [InlineData("La Paz", 22)]
    [InlineData("La Unión", 18)]
    [InlineData("Morazán", 26)]
    [InlineData("San Miguel", 20)]
    [InlineData("San Salvador", 19)]
    [InlineData("San Vicente", 13)]
    [InlineData("Santa Ana", 13)]
    [InlineData("Sonsonate", 16)]
    [InlineData("Usulután", 23)]
    public void CadaDepartamentoTieneSusMunicipiosSinRepetir(string departamento, int cantidad)
    {
        var municipios = Territorio.Municipios[departamento];

        Assert.Equal(cantidad, municipios.Length);
        Assert.Equal(cantidad, municipios.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("Sonsonate", "Izalco", true)]
    [InlineData("Sonsonate", "Santa Tecla", false)]
    [InlineData("Morazán", "El Rosario", true)]
    [InlineData("Narnia", "Sonsonate", false)]
    [InlineData(null, "Sonsonate", false)]
    public void ValidaQueElMunicipioSeaDelDepartamento(string? departamento, string municipio, bool existe) =>
        Assert.Equal(existe, Territorio.Existe(departamento, municipio));

    [Fact]
    public void LosNivelesSeOrdenanDeAbajoHaciaArriba()
    {
        var unidades = new[] { "Nivel 10", "Planta baja", "Nivel 2", "Sótano 1", null, "Otro" }
            .Select((nivel, i) => new UnidadDetalle { Propiedad = "P", Codigo = $"U{i}", Nivel = nivel });

        var orden = Catalogos.Ordenar(unidades).Select(u => u.Nivel).ToArray();

        Assert.Equal(new[] { null, "Sótano 1", "Planta baja", "Nivel 2", "Nivel 10", "Otro" }, orden);
    }

    [Fact]
    public void LosCodigosSeOrdenanComoNumeros()
    {
        string[] codigos = ["L-10", "L-2", "L-1"];
        string[] esperado = ["L-1", "L-2", "L-10"];
        var unidades = codigos.Select(c => new UnidadDetalle { Propiedad = "P", Codigo = c });

        Assert.Equal(esperado, Catalogos.Ordenar(unidades).Select(u => u.Codigo));
    }
}
