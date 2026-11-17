using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Servicios;
using Portal.Sesion;

namespace Portal.Pages;

/// <summary>
/// Página de error. Muestra el mensaje del servicio con su estado HTTP (403, 404, 409, 503…).
/// Si el núcleo dijo que el token venció (401), cierra la sesión del portal y pide entrar otra vez.
/// </summary>
[IgnoreAntiforgeryToken]
public class ErrorModel(AlmacenSesiones almacen) : PageModel
{
    public int Estado { get; private set; }
    public string Mensaje { get; private set; } = "Ocurrió un error inesperado.";

    public Task<IActionResult> OnGetAsync() => Atender();

    public Task<IActionResult> OnPostAsync() => Atender();

    private async Task<IActionResult> Atender()
    {
        var error = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (error is ErrorServicio { Estado: 401 })
        {
            var sesion = SesionUsuario.De(User);
            if (sesion is not null) almacen.Quitar(sesion.SesionId);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/login?vencida");
        }
        if (error is ErrorServicio e)
        {
            Estado = e.Estado is >= 400 and < 600 ? e.Estado : 500;
            Mensaje = e.ParaUsuario();
        }
        else if (error is not null)
        {
            Estado = 500;
        }
        if (Estado > 0) Response.StatusCode = Estado;
        return Page();
    }
}
