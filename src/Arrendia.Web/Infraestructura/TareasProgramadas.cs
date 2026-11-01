using Arrendia.Web.Data;
using Arrendia.Web.Services;
using Dapper;

namespace Arrendia.Web.Infraestructura;

public sealed partial class ProcesosDiarios(IServiceScopeFactory alcances, ILogger<ProcesosDiarios> registro) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cancelacion = stoppingToken;
        await Task.Delay(TimeSpan.FromSeconds(20), cancelacion);
        using var reloj = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var alcance = alcances.CreateAsyncScope();
                var baseDatos = alcance.ServiceProvider.GetRequiredService<IBaseDatos>();
                await using var conexion = await baseDatos.AbrirAsync(cancelacion);
                await conexion.ExecuteAsync(new CommandDefinition("dbo.SP_EJECUTAR_PROCESOS_DIARIOS",
                    commandType: System.Data.CommandType.StoredProcedure, commandTimeout: 600, cancellationToken: cancelacion));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Fallo(registro, error);
            }
        }
        while (await reloj.WaitForNextTickAsync(cancelacion));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falló el proceso diario de cargos y mora")]
    private static partial void Fallo(ILogger registro, Exception error);
}

public sealed partial class ProcesadorCorreos(IServiceScopeFactory alcances, ILogger<ProcesadorCorreos> registro) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cancelacion = stoppingToken;
        await Task.Delay(TimeSpan.FromSeconds(30), cancelacion);
        using var reloj = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var alcance = alcances.CreateAsyncScope();
                await ProcesarAsync(alcance.ServiceProvider, cancelacion);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                FalloLote(registro, error);
            }
        }
        while (await reloj.WaitForNextTickAsync(cancelacion));
    }

    private async Task ProcesarAsync(IServiceProvider servicios, CancellationToken cancelacion)
    {
        var correo = servicios.GetRequiredService<ICorreo>();
        var cola = servicios.GetRequiredService<IRepositorioCorreos>();
        await cola.ExpirarSinEnlaceAsync(cancelacion);
        if (!correo.PuedeEnviar)
        {
            return;
        }

        var documentos = servicios.GetRequiredService<ServicioDocumentos>();
        var datos = servicios.GetRequiredService<IRepositorioDocumentos>();
        var almacen = servicios.GetRequiredService<IAlmacenArchivos>();
        foreach (var fila in await cola.ParaEnviarAsync(20, cancelacion))
        {
            try
            {
                var mensaje = fila.Tipo == "AvisoCobro"
                    ? await AvisoAsync(fila, cola, documentos, datos, almacen, cancelacion)
                    : await ComprobanteAsync(fila, cola, documentos, datos, almacen, cancelacion);
                if (mensaje is null)
                {
                    await cola.MarcarAsync(fila.Id, false, "El cargo o el pago ya no existe.", cancelacion);
                    continue;
                }

                await correo.EnviarAsync(mensaje, cancelacion);
                await cola.MarcarAsync(fila.Id, true, null, cancelacion);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                FalloCorreo(registro, fila.Id, error);
                await cola.MarcarAsync(fila.Id, false, error.Message, cancelacion);
            }
        }
    }

    private static async Task<MensajeCorreo?> AvisoAsync(CorreoCola fila, IRepositorioCorreos cola, ServicioDocumentos documentos,
        IRepositorioDocumentos datos, IAlmacenArchivos almacen, CancellationToken cancelacion)
    {
        if (fila.CargoId is not { } cargoId || fila.ContratoId is not { } contratoId)
        {
            return null;
        }

        var aviso = await datos.DatosAvisoAsync(cargoId, cancelacion);
        if (aviso is null)
        {
            return null;
        }

        var documentoId = fila.DocumentoGeneradoId ?? await cola.DocumentoDeCargoAsync(cargoId, cancelacion);
        byte[]? pdf;
        if (documentoId is { } existente)
        {
            pdf = await LeerAsync(existente, cola, almacen, cancelacion);
        }
        else
        {
            var emitido = await documentos.AvisoCobroAsync(cargoId, contratoId, cancelacion);
            documentoId = emitido?.Id;
            pdf = emitido?.Pdf;
        }

        if (documentoId is { } id && fila.DocumentoGeneradoId is null)
        {
            await cola.VincularDocumentoAsync(fila.Id, id, cancelacion);
        }

        return PlantillasCorreo.AvisoCobro(fila.Destinatario, fila.Asunto, aviso.Inquilino, aviso.Descripcion.ToLowerInvariant(), aviso.Monto,
            aviso.FechaVencimiento, pdf is null ? null : new Adjunto($"aviso-{aviso.Contrato}.pdf", pdf, "application/pdf"));
    }

    private static async Task<MensajeCorreo?> ComprobanteAsync(CorreoCola fila, IRepositorioCorreos cola, ServicioDocumentos documentos,
        IRepositorioDocumentos datos, IAlmacenArchivos almacen, CancellationToken cancelacion)
    {
        if (fila.PagoId is not { } pagoId)
        {
            return null;
        }

        var pago = await datos.DatosComprobanteAsync(pagoId, cancelacion);
        if (pago is null)
        {
            return null;
        }

        var documentoId = fila.DocumentoGeneradoId ?? await datos.ComprobanteDePagoAsync(pagoId, cancelacion);
        byte[]? pdf;
        if (documentoId is { } existente)
        {
            pdf = await LeerAsync(existente, cola, almacen, cancelacion);
        }
        else
        {
            var emitido = await documentos.ComprobanteAsync(pagoId, null, cancelacion);
            documentoId = emitido?.Id;
            pdf = emitido?.Pdf;
        }

        if (documentoId is { } id && fila.DocumentoGeneradoId is null)
        {
            await cola.VincularDocumentoAsync(fila.Id, id, cancelacion);
        }

        return PlantillasCorreo.Comprobante(fila.Destinatario, fila.Asunto, pago.Inquilino, pago.Numero, pago.Monto, pago.Metodo,
            pdf is null ? null : new Adjunto($"comprobante-{pago.Numero}.pdf", pdf, "application/pdf"));
    }

    private static async Task<byte[]?> LeerAsync(int documentoId, IRepositorioCorreos cola, IAlmacenArchivos almacen, CancellationToken cancelacion)
    {
        var archivo = await cola.ArchivoDeDocumentoAsync(documentoId, cancelacion);
        if (archivo is null)
        {
            return null;
        }

        await using var flujo = await almacen.AbrirAsync(archivo.PublicId, archivo.TipoMime, cancelacion);
        if (flujo is null)
        {
            return null;
        }

        using var memoria = new MemoryStream();
        await flujo.CopyToAsync(memoria, cancelacion);
        return memoria.ToArray();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falló el envío de la cola de correos")]
    private static partial void FalloLote(ILogger registro, Exception error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo enviar el correo {CorreoId}")]
    private static partial void FalloCorreo(ILogger registro, long correoId, Exception error);
}
