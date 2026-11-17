using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.WebEncoders;
using Portal.Servicios;
using Portal.Sesion;

// UD-09 Portal web en ASP.NET Core Razor Pages. Cada pantalla es una página (.cshtml) con su
// PageModel: el PageModel expone los datos que la vista enlaza y atiende los formularios
// (OnGet/OnPost). El portal no guarda datos propios: consume los servicios ya desplegados
// (núcleo por REST, servidor por REST, Reconstrucción por gRPC y el servicio de tablero por REST).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);
string Url(string clave, string defecto) => (builder.Configuration[clave] ?? defecto).TrimEnd('/') + "/";

builder.Services.AddRazorPages(o =>
{
    // Todo el portal exige sesión, salvo el inicio de sesión y la página de error.
    o.Conventions.AuthorizeFolder("/");
    o.Conventions.AllowAnonymousToPage("/Login");
    o.Conventions.AllowAnonymousToPage("/Error");
});
// Español sin entidades: "á", "ñ" salen tal cual en el HTML (Razor las escribiría como &#xE1;).
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Sesión: la cookie (HttpOnly) solo lleva la identidad y un id de sesión. El token del núcleo se
// queda en la memoria del portal (AlmacenSesiones) y nunca llega al navegador.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<AlmacenSesiones>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "pgdr.sesion";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.LoginPath = "/login";
        o.ReturnUrlParameter = "siguiente";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = false;
        o.Events.OnRedirectToLogin = ctx =>
        {
            // El tablero es JSON para el navegador: sin sesión responde 401 en vez de redirigir.
            if (ctx.Request.Path.StartsWithSegments("/tablero")) ctx.Response.StatusCode = 401;
            else ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        o.Events.OnValidatePrincipal = async ctx =>
        {
            // Si el token ya no está (el portal se reinició o la sesión se cerró), se pide entrar otra vez.
            var id = ctx.Principal is null ? null : SesionUsuario.De(ctx.Principal)?.SesionId;
            var almacen = ctx.HttpContext.RequestServices.GetRequiredService<AlmacenSesiones>();
            if (id is null || almacen.Token(id) is null)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();

// Clientes de los servicios. Límites cortos: si un servicio no responde, el portal lo dice.
static SocketsHttpHandler Conexion() => new() { ConnectTimeout = TimeSpan.FromSeconds(2), AllowAutoRedirect = false };
builder.Services.AddHttpClient<NucleoCliente>(c =>
{
    c.BaseAddress = new Uri(Url("PORTAL_NUCLEO_URL", "http://localhost:18092/plataforma-desastres/api"));
    c.Timeout = TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(Conexion);
builder.Services.AddHttpClient<ServidorCliente>(c =>
{
    c.BaseAddress = new Uri(Url("PORTAL_SERVIDOR_URL", "http://localhost:18085"));
    c.Timeout = TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(Conexion);
builder.Services.AddHttpClient<TableroCliente>(c =>
{
    // El servicio de tablero responde en máximo 3 s (su límite total); un segundo más de margen.
    c.BaseAddress = new Uri(Url("PORTAL_TABLERO_URL", "http://localhost:18093"));
    c.Timeout = TimeSpan.FromSeconds(4);
}).ConfigurePrimaryHttpMessageHandler(Conexion);
builder.Services.AddSingleton(_ => GrpcChannel.ForAddress(Url("PORTAL_RECONSTRUCCION_URL", "http://localhost:19096")));
builder.Services.AddSingleton<ReconstruccionCliente>();

var app = builder.Build();

app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/salud", () => "ok").AllowAnonymous();

// El mapa del tablero pide sus datos aquí. El portal reenvía la consulta al servicio de tablero
// (servlet asíncrono multihilo, ADR-24) con el token del usuario, que el navegador no conoce.
app.MapGet("/tablero", async (HttpContext ctx, TableroCliente tablero, AlmacenSesiones almacen) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    var sesion = SesionUsuario.De(ctx.User);
    var token = sesion is null ? null : almacen.Token(sesion.SesionId);
    if (sesion is null || token is null)
        return Results.Json(new { error = "Inicia sesión para ver el tablero." }, statusCode: 401);
    try
    {
        var r = await tablero.Consultar(token, ctx.Request.Query["desastre"].ToString(), ctx.Request.Query["modo"].ToString());
        if (r.Estado == 401)
        {
            almacen.Quitar(sesion.SesionId);
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        return Results.Content(Encoding.UTF8.GetString(r.Bytes), "application/json; charset=utf-8", Encoding.UTF8, r.Estado);
    }
    catch (ErrorServicio e)
    {
        return Results.Json(new { error = e.Message }, statusCode: e.Estado);
    }
}).RequireAuthorization();

app.MapRazorPages();
app.Run();
