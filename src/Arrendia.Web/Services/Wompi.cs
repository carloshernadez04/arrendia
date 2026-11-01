using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Arrendia.Web.Data;
using Microsoft.Extensions.Options;

namespace Arrendia.Web.Services;

public sealed class OpcionesWompi
{
    public const string Seccion = "Wompi";

    public string? AppId { get; set; }
    public string? ApiSecret { get; set; }
    public string UrlToken { get; set; } = "https://id.wompi.sv/connect/token";
    public string UrlApi { get; set; } = "https://api.wompi.sv";
    public string Audiencia { get; set; } = "wompi_api";
    public bool PermitirProductivo { get; set; }

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(ApiSecret);
}

public sealed record AvisoPasarela(string IdTransaccion, string Referencia, bool Aprobado, decimal Monto, string? MedioPago, DateTime Fecha, bool EsProductivo);

public interface IPasarelaPago
{
    bool Disponible { get; }
    Task<string> CrearEnlaceAsync(string referencia, decimal monto, string descripcion, string urlRetorno, string urlAviso, CancellationToken cancelacion);
    bool FirmaValida(string cuerpo, string? firma);
}

public static class FirmaWompi
{
    public static string Calcular(string cuerpo, string secreto) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes(cuerpo)));

    public static bool Verificar(string cuerpo, string? firma, string? secreto)
    {
        if (string.IsNullOrEmpty(firma) || string.IsNullOrEmpty(secreto))
        {
            return false;
        }

        var esperada = Encoding.ASCII.GetBytes(Calcular(cuerpo, secreto));
        var recibida = Encoding.ASCII.GetBytes(firma.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(esperada, recibida);
    }

    public static AvisoPasarela? Leer(string cuerpo)
    {
        try
        {
            using var json = JsonDocument.Parse(cuerpo);
            var raiz = json.RootElement;
            var referencia = raiz.TryGetProperty("EnlacePago", out var enlace) && enlace.ValueKind == JsonValueKind.Object
                && enlace.TryGetProperty("IdentificadorEnlaceComercio", out var r) ? r.GetString() : null;
            var id = raiz.TryGetProperty("IdTransaccion", out var t) ? t.ToString() : null;
            if (string.IsNullOrEmpty(referencia) || string.IsNullOrEmpty(id) || !raiz.TryGetProperty("Monto", out var monto))
            {
                return null;
            }

            var resultado = raiz.TryGetProperty("ResultadoTransaccion", out var res) ? res.GetString() : null;
            var fecha = raiz.TryGetProperty("FechaTransaccion", out var f) && DateTime.TryParse(f.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var valor) ? valor : DateTime.UtcNow;
            return new AvisoPasarela(id, referencia, string.Equals(resultado, "ExitosaAprobada", StringComparison.OrdinalIgnoreCase),
                monto.ValueKind == JsonValueKind.Number ? monto.GetDecimal() : decimal.Parse(monto.GetString() ?? "0", CultureInfo.InvariantCulture),
                raiz.TryGetProperty("FormaPagoUtilizada", out var medio) ? medio.GetString() : null, fecha,
                raiz.TryGetProperty("EsProductiva", out var prod) && prod.ValueKind == JsonValueKind.True);
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }
}

public sealed class PasarelaWompi(IOptions<OpcionesWompi> opciones, IHttpClientFactory clientes) : IPasarelaPago
{
    private readonly OpcionesWompi _opciones = opciones.Value;

    public bool Disponible => true;

    public async Task<string> CrearEnlaceAsync(string referencia, decimal monto, string descripcion, string urlRetorno, string urlAviso, CancellationToken cancelacion)
    {
        var cliente = clientes.CreateClient(nameof(PasarelaWompi));
        using var pedirToken = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _opciones.AppId!,
            ["client_secret"] = _opciones.ApiSecret!,
            ["audience"] = _opciones.Audiencia,
        });
        using var respuestaToken = await cliente.PostAsync(new Uri(_opciones.UrlToken), pedirToken, cancelacion);
        respuestaToken.EnsureSuccessStatusCode();
        var token = (await respuestaToken.Content.ReadFromJsonAsync<JsonElement>(cancelacion)).GetProperty("access_token").GetString();

        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(_opciones.UrlApi), "/EnlacePago"))
        {
            Content = JsonContent.Create(new
            {
                identificadorEnlaceComercio = referencia,
                monto,
                nombreProducto = descripcion,
                formaPago = new { permitirTarjetaCreditoDebido = true, permitirPagoConPuntoAgricola = false, permitirPagoEnCuotasAgricola = false },
                configuracion = new
                {
                    urlRedirect = urlRetorno,
                    urlWebhook = urlAviso,
                    esMontoEditable = false,
                    esCantidadEditable = false,
                    cantidadPorDefecto = 1,
                    duracionInterfazIntentoMinutos = 30,
                    notificarTransaccionCliente = true,
                },
                limitesDeUso = new { cantidadMaximaPagosExitosos = 1, cantidadMaximaPagosFallidos = 5 },
            }),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuesta = await cliente.SendAsync(pedido, cancelacion);
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>(cancelacion);
        return cuerpo.GetProperty("urlEnlace").GetString() ?? throw new InvalidOperationException("Wompi no devolvió el enlace de pago.");
    }

    public bool FirmaValida(string cuerpo, string? firma) => FirmaWompi.Verificar(cuerpo, firma, _opciones.ApiSecret);
}

public sealed class PasarelaSimulada(IHostEnvironment ambiente) : IPasarelaPago
{
    public const string Secreto = "simulador-de-desarrollo";

    public bool Disponible => ambiente.IsDevelopment();

    public Task<string> CrearEnlaceAsync(string referencia, decimal monto, string descripcion, string urlRetorno, string urlAviso, CancellationToken cancelacion) =>
        Task.FromResult($"/portal/pago/simulador/{referencia}");

    public bool FirmaValida(string cuerpo, string? firma) => ambiente.IsDevelopment() && FirmaWompi.Verificar(cuerpo, firma, Secreto);
}

public sealed partial class ProcesadorAvisos(IRepositorioPortal repositorio, IPasarelaPago pasarela, IOptions<OpcionesWompi> opciones, ILogger<ProcesadorAvisos> registro)
{
    public async Task<string> ProcesarAsync(string cuerpo, string? firma, CancellationToken cancelacion)
    {
        var aviso = FirmaWompi.Leer(cuerpo);
        var valida = pasarela.FirmaValida(cuerpo, firma);
        var eventoId = await repositorio.GuardarAvisoAsync(aviso?.IdTransaccion, JsonValido(cuerpo), valida, cancelacion);
        if (!valida || aviso is null)
        {
            AvisoRechazado(registro, eventoId);
            await repositorio.MarcarAvisoAsync(eventoId, valida ? "El aviso no trae los datos esperados." : "La firma del aviso no es válida.", cancelacion);
            return "rechazado";
        }

        if (aviso.EsProductivo && !opciones.Value.PermitirProductivo)
        {
            await repositorio.MarcarAvisoAsync(eventoId, "Pago productivo recibido en un ambiente de pruebas: no se aplica.", cancelacion);
            return "ignorado";
        }

        return await repositorio.ProcesarAvisoAsync(eventoId, aviso, cuerpo, cancelacion);
    }

    private static string JsonValido(string cuerpo)
    {
        try
        {
            using var _ = JsonDocument.Parse(cuerpo);
            return cuerpo;
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new { contenido = cuerpo.Length > 4000 ? cuerpo[..4000] : cuerpo });
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Aviso de pago rechazado {EventoId}")]
    private static partial void AvisoRechazado(ILogger registro, long eventoId);
}
