using System.Security.Cryptography;
using Arrendia.Web.Data;

namespace Arrendia.Web.Services;

public sealed record DocumentoEmitido(int Id, string Folio, byte[] Pdf);

public sealed class ServicioDocumentos(
    IRepositorioDocumentos repositorio,
    IAlmacenArchivos almacen,
    IConfiguration configuracion,
    IHttpContextAccessor contexto)
{
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string UrlPublica()
    {
        if (configuracion["App:UrlPublica"] is { Length: > 0 } url)
        {
            return url.TrimEnd('/');
        }

        var peticion = contexto.HttpContext?.Request;
        return peticion is null ? "https://arrendia.carloshernandez.site" : $"{peticion.Scheme}://{peticion.Host}";
    }

    public async Task<DocumentoEmitido?> ContratoAsync(int contratoId, int usuarioId, CancellationToken cancelacion)
    {
        var datos = await repositorio.DatosContratoAsync(contratoId, cancelacion);
        return datos is null
            ? null
            : await EmitirAsync("Contrato", $"contrato-{datos.Numero}.pdf", contratoId, null, usuarioId, e => PlantillasPdf.Contrato(datos, e), cancelacion);
    }

    public async Task<DocumentoEmitido?> ComprobanteAsync(int pagoId, int? usuarioId, CancellationToken cancelacion)
    {
        var datos = await repositorio.DatosComprobanteAsync(pagoId, cancelacion);
        if (datos is null)
        {
            return null;
        }

        var usuario = usuarioId ?? await repositorio.IdSistemaAsync(cancelacion);
        return await EmitirAsync("Comprobante", $"comprobante-{datos.Numero}.pdf", null, pagoId, usuario, e => PlantillasPdf.Comprobante(datos, e), cancelacion);
    }

    public async Task<DocumentoEmitido?> AvisoCobroAsync(int cargoId, int contratoId, CancellationToken cancelacion)
    {
        var datos = await repositorio.DatosAvisoAsync(cargoId, cancelacion);
        if (datos is null)
        {
            return null;
        }

        var sistema = await repositorio.IdSistemaAsync(cancelacion);
        return await EmitirAsync("AvisoCobro", $"aviso-{datos.Contrato}-{cargoId}.pdf", contratoId, null, sistema, e => PlantillasPdf.AvisoCobro(datos, e), cancelacion);
    }

    public async Task<DocumentoEmitido> EstadoCuentaAsync(int contratoId, DateTime desde, DateTime hasta, int usuarioId, CancellationToken cancelacion)
    {
        var datos = await repositorio.EstadoCuentaAsync(contratoId, desde, hasta, usuarioId, cancelacion);
        return await EmitirAsync("EstadoCuenta", $"estado-de-cuenta-{datos.Contrato}.pdf", contratoId, null, usuarioId, e => PlantillasPdf.EstadoCuenta(datos, e), cancelacion);
    }

    private async Task<DocumentoEmitido> EmitirAsync(string tipo, string nombre, int? contratoId, int? pagoId, int usuarioId,
        Func<Emision, byte[]> generar, CancellationToken cancelacion)
    {
        var empresa = await repositorio.EmpresaAsync(cancelacion);
        var folio = await repositorio.ReservarFolioAsync(cancelacion);
        var codigo = RandomNumberGenerator.GetString(Alfabeto, 12);
        var pdf = generar(new Emision(folio, codigo, $"{UrlPublica()}/verificar/{codigo}", empresa.Nombre, empresa.Nit));
        string publicId;
        using (var flujo = new MemoryStream(pdf))
        {
            publicId = await almacen.GuardarAsync(flujo, "application/pdf", "documentos", cancelacion);
        }

        var id = await repositorio.RegistrarAsync(folio, tipo, codigo, SHA256.HashData(pdf), publicId, nombre, pdf.Length, contratoId, pagoId, usuarioId, cancelacion);
        return new DocumentoEmitido(id, folio, pdf);
    }
}
