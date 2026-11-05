using System.Text;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Http;

namespace Arrendia.Tests;

public class ArchivosTests
{
    private static FormFile Archivo(byte[] datos, string nombre) => new(new MemoryStream(datos), 0, datos.Length, "archivos", nombre);

    [Fact]
    public async Task SinArchivosNoHayNadaQueGuardar()
    {
        var (archivo, error) = await TiposArchivo.PrepararAsync([], "respaldo", CancellationToken.None);

        Assert.Null(archivo);
        Assert.Null(error);
    }

    [Fact]
    public async Task UnaSolaFotoSeGuardaTalCual()
    {
        var foto = PlantillasPdf.Qr("una");

        var (archivo, error) = await TiposArchivo.PrepararAsync([Archivo(foto, "recibo.png")], "respaldo", CancellationToken.None);

        Assert.Null(error);
        Assert.Equal("image/png", archivo!.Tipo);
        Assert.Equal("recibo.png", archivo.Nombre);
        Assert.Equal(foto, archivo.Datos);
    }

    [Fact]
    public async Task VariasFotosSeUnenEnUnPdf()
    {
        PlantillasPdf.Configurar();

        var (archivo, error) = await TiposArchivo.PrepararAsync(
            [Archivo(PlantillasPdf.Qr("pagina 1"), "1.png"), Archivo(PlantillasPdf.Qr("pagina 2"), "2.png")], "contrato-firmado-C-1", CancellationToken.None);

        Assert.Null(error);
        Assert.Equal("application/pdf", archivo!.Tipo);
        Assert.Equal("contrato-firmado-C-1.pdf", archivo.Nombre);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(archivo.Datos, 0, 5), StringComparison.Ordinal);
        Assert.Equal(2, CuantasPaginas(archivo.Datos));
    }

    [Fact]
    public async Task NoMezclaFotosConUnPdf()
    {
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7 prueba");

        var (archivo, error) = await TiposArchivo.PrepararAsync([Archivo(PlantillasPdf.Qr("x"), "1.png"), Archivo(pdf, "2.pdf")], "respaldo", CancellationToken.None);

        Assert.Null(archivo);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task RechazaMasDeDiezArchivos()
    {
        var foto = PlantillasPdf.Qr("x");
        var muchos = Enumerable.Range(1, TiposArchivo.MaximoPorEnvio + 1).Select(i => (IFormFile)Archivo(foto, $"{i}.png")).ToList();

        var (archivo, error) = await TiposArchivo.PrepararAsync(muchos, "respaldo", CancellationToken.None);

        Assert.Null(archivo);
        Assert.Contains("10", error, StringComparison.Ordinal);
    }

    private static int CuantasPaginas(byte[] pdf) =>
        System.Text.RegularExpressions.Regex.Count(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page[^s]");
}
