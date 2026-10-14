using System.Globalization;
using System.Threading.RateLimiting;
using Arrendia.Web.Controllers;
using Arrendia.Web.Data;
using Arrendia.Web.Infraestructura;
using Arrendia.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var desarrollo = builder.Environment.IsDevelopment();

builder.WebHost.ConfigureKestrel(opciones => opciones.AddServerHeader = false);

BaseDatos.Configurar();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBaseDatos, BaseDatos>();
builder.Services.AddScoped<IRepositorioCuentas, RepositorioCuentas>();
builder.Services.AddScoped<IPasswordHasher<UsuarioAcceso>, PasswordHasher<UsuarioAcceso>>();
builder.Services.AddScoped<ServicioAcceso>();
builder.Services.AddScoped<ValidacionSesion>();

builder.Services.Configure<OpcionesSmtp>(builder.Configuration.GetSection(OpcionesSmtp.Seccion));
if (builder.Configuration.GetSection(OpcionesSmtp.Seccion).Get<OpcionesSmtp>()?.EstaConfigurado == true)
{
    builder.Services.AddScoped<ICorreo, CorreoSmtp>();
}
else
{
    builder.Services.AddScoped<ICorreo, CorreoEnRegistro>();
}

var proteccion = builder.Services.AddDataProtection().SetApplicationName("Arrendia");
if (builder.Configuration["DataProtection:Ruta"] is { Length: > 0 } rutaClaves)
{
    proteccion.PersistKeysToFileSystem(new DirectoryInfo(rutaClaves));
}

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "arrendia.sesion";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SameSite = SameSiteMode.Lax;
        opciones.Cookie.SecurePolicy = desarrollo ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
        opciones.LoginPath = "/cuenta/entrar";
        opciones.LogoutPath = "/cuenta/salir";
        opciones.AccessDeniedPath = "/cuenta/acceso-denegado";
        opciones.ReturnUrlParameter = "volver";
        opciones.EventsType = typeof(ValidacionSesion);
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddAntiforgery(opciones =>
{
    opciones.Cookie.Name = "arrendia.af";
    opciones.Cookie.SecurePolicy = desarrollo ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});

builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy(CuentaController.PoliticaLimite, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllersWithViews(opciones => opciones.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var salud = builder.Services.AddHealthChecks();
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString(BaseDatos.NombreCadena)))
{
    salud.AddCheck<SaludBaseDatos>("base-de-datos");
}

builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
{
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opciones.KnownIPNetworks.Clear();
    opciones.KnownProxies.Clear();
});

var app = builder.Build();

if (args.Length > 0 && args[0] == ComandoActivarAdministrador.Nombre)
{
    return await ComandoActivarAdministrador.EjecutarAsync(app.Services, args.ElementAtOrDefault(1));
}

var cultura = new CultureInfo("es-SV");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultura),
    SupportedCultures = [cultura],
    SupportedUICultures = [cultura],
});

app.UseForwardedHeaders();

if (!desarrollo)
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (contexto, siguiente) =>
{
    var encabezados = contexto.Response.Headers;
    encabezados["X-Content-Type-Options"] = "nosniff";
    encabezados["X-Frame-Options"] = "DENY";
    encabezados["Referrer-Policy"] = "strict-origin-when-cross-origin";
    encabezados["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    encabezados["Cross-Origin-Opener-Policy"] = "same-origin";
    encabezados["Cross-Origin-Embedder-Policy"] = "credentialless";
    encabezados["Cross-Origin-Resource-Policy"] = "same-origin";
    encabezados["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data: https://res.cloudinary.com; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    contexto.Response.OnStarting(() =>
    {
        var cache = encabezados.CacheControl.ToString();
        if (cache.Length == 0 || cache.Contains("no-store", StringComparison.OrdinalIgnoreCase))
        {
            encabezados.CacheControl = "no-cache, no-store, must-revalidate";
            encabezados.Pragma = "no-cache";
        }

        return Task.CompletedTask;
    });
    await siguiente();
});

app.UseStatusCodePagesWithReExecute("/error");
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapGet("/robots.txt", () => Results.Text("User-agent: *\nDisallow: /\n", "text/plain")).AllowAnonymous();
app.MapControllers().WithStaticAssets();
app.MapFallback(() => Results.NotFound()).AllowAnonymous();

await app.RunAsync();
return 0;

public partial class Program;
