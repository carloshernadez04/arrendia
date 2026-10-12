using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// No revelar el servidor web en las respuestas
builder.WebHost.ConfigureKestrel(opciones => opciones.AddServerHeader = false);

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

// La app solo escucha en 127.0.0.1 detrás de Cloudflare Tunnel: se confía en el esquema original (https)
builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
{
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opciones.KnownIPNetworks.Clear();
    opciones.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Encabezados de seguridad en todas las respuestas
app.Use(async (contexto, siguiente) =>
{
    var encabezados = contexto.Response.Headers;
    encabezados["X-Content-Type-Options"] = "nosniff";
    encabezados["X-Frame-Options"] = "DENY";
    encabezados["Referrer-Policy"] = "strict-origin-when-cross-origin";
    encabezados["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    encabezados["Cross-Origin-Opener-Policy"] = "same-origin";
    encabezados["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data: https://res.cloudinary.com; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await siguiente();
});
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Permite que las pruebas levanten la aplicación con WebApplicationFactory
public partial class Program;
