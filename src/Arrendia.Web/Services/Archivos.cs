using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace Arrendia.Web.Services;

public sealed class OpcionesCloudinary
{
    public const string Seccion = "Cloudinary";

    public string? CloudName { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string Carpeta { get; set; } = "arrendia";

    public bool EstaConfigurado =>
        !string.IsNullOrWhiteSpace(CloudName) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}

public sealed class OpcionesArchivos
{
    public const string Seccion = "Archivos";

    public string? RutaLocal { get; set; }
}

public interface IAlmacenArchivos
{
    Task<string> GuardarAsync(Stream contenido, string tipoMime, string carpeta, CancellationToken cancelacion);
    Task<Stream?> AbrirAsync(string publicId, string tipoMime, CancellationToken cancelacion);
}

public static class TiposArchivo
{
    public const long TamanoMaximo = 10 * 1024 * 1024;
    public static readonly string[] Imagenes = ["image/jpeg", "image/png", "image/webp"];
    public static readonly string[] ImagenesYPdf = ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    public static string Extension(string tipoMime) => tipoMime switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".pdf",
    };

    public static string? Detectar(ReadOnlySpan<byte> inicio)
    {
        if (inicio.Length >= 3 && inicio[0] == 0xFF && inicio[1] == 0xD8 && inicio[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (inicio.Length >= 8 && inicio[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (inicio.Length >= 12 && inicio[..4].SequenceEqual("RIFF"u8) && inicio[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        if (inicio.Length >= 5 && inicio[..5].SequenceEqual("%PDF-"u8))
        {
            return "application/pdf";
        }

        return null;
    }

    public static async Task<(string? TipoMime, string? Error)> ValidarAsync(IFormFile? archivo, string[] permitidos, CancellationToken cancelacion)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return (null, "Elige un archivo.");
        }

        if (archivo.Length > TamanoMaximo)
        {
            return (null, "El archivo no puede pasar de 10 MB.");
        }

        var cabecera = new byte[16];
        await using var flujo = archivo.OpenReadStream();
        var leidos = await flujo.ReadAtLeastAsync(cabecera, cabecera.Length, false, cancelacion);
        var tipo = Detectar(cabecera.AsSpan(0, leidos));
        if (tipo is null || !permitidos.Contains(tipo))
        {
            return (null, permitidos.Contains("application/pdf")
                ? "Solo se aceptan fotos (JPG, PNG o WEBP) o PDF."
                : "Solo se aceptan fotos JPG, PNG o WEBP.");
        }

        return (tipo, null);
    }
}

public sealed class AlmacenLocal(IOptions<OpcionesArchivos> opciones, IHostEnvironment ambiente) : IAlmacenArchivos
{
    private readonly string _raiz = Path.GetFullPath(string.IsNullOrWhiteSpace(opciones.Value.RutaLocal)
        ? Path.Combine(ambiente.ContentRootPath, "App_Data", "archivos")
        : opciones.Value.RutaLocal);

    public async Task<string> GuardarAsync(Stream contenido, string tipoMime, string carpeta, CancellationToken cancelacion)
    {
        var nombre = $"{Guid.NewGuid():N}{TiposArchivo.Extension(tipoMime)}";
        var directorio = Path.Combine(_raiz, carpeta);
        Directory.CreateDirectory(directorio);
        await using var destino = File.Create(Path.Combine(directorio, nombre));
        await contenido.CopyToAsync(destino, cancelacion);
        return $"local/{carpeta}/{nombre}";
    }

    public Task<Stream?> AbrirAsync(string publicId, string tipoMime, CancellationToken cancelacion)
    {
        if (!publicId.StartsWith("local/", StringComparison.Ordinal))
        {
            return Task.FromResult<Stream?>(null);
        }

        var ruta = Path.GetFullPath(Path.Combine(_raiz, publicId["local/".Length..]));
        if (!ruta.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(ruta))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(ruta));
    }
}

public sealed class AlmacenCloudinary(IOptions<OpcionesCloudinary> opciones, IHttpClientFactory clientes, AlmacenLocal local) : IAlmacenArchivos
{
    private readonly OpcionesCloudinary _opciones = opciones.Value;
    private readonly Cloudinary _cloudinary = new(new Account(opciones.Value.CloudName, opciones.Value.ApiKey, opciones.Value.ApiSecret)) { Api = { Secure = true } };

    public async Task<string> GuardarAsync(Stream contenido, string tipoMime, string carpeta, CancellationToken cancelacion)
    {
        var parametros = new ImageUploadParams
        {
            File = new FileDescription($"archivo{TiposArchivo.Extension(tipoMime)}", contenido),
            Folder = $"{_opciones.Carpeta}/{carpeta}",
            Type = "private",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false,
        };
        var resultado = await _cloudinary.UploadAsync(parametros, cancelacion);
        if (resultado.Error is not null || string.IsNullOrEmpty(resultado.PublicId))
        {
            throw new InvalidOperationException($"No se pudo subir el archivo: {resultado.Error?.Message}");
        }

        return resultado.PublicId;
    }

    public async Task<Stream?> AbrirAsync(string publicId, string tipoMime, CancellationToken cancelacion)
    {
        if (publicId.StartsWith("local/", StringComparison.Ordinal))
        {
            return await local.AbrirAsync(publicId, tipoMime, cancelacion);
        }

        var formato = TiposArchivo.Extension(tipoMime).TrimStart('.');
        var vence = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var url = _cloudinary.DownloadPrivate(publicId, false, formato, "private", vence, "image");
        var cliente = clientes.CreateClient(nameof(AlmacenCloudinary));
        var respuesta = await cliente.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            respuesta.Dispose();
            return null;
        }

        return await respuesta.Content.ReadAsStreamAsync(cancelacion);
    }
}
