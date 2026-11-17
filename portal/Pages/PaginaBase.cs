using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Servicios;
using Portal.Sesion;

namespace Portal.Pages;

/// <summary>
/// Lo común a las páginas con sesión: quién es el usuario, su token (guardado en el portal) y la
/// forma de atender un formulario que llama a un servicio y vuelve con un mensaje.
/// </summary>
public abstract class PaginaBase : PageModel
{
    public SesionUsuario Sesion => SesionUsuario.De(User) ?? throw new ErrorServicio(401, "Inicia sesión.");

    protected string Token =>
        HttpContext.RequestServices.GetRequiredService<AlmacenSesiones>().Token(Sesion.SesionId)
        ?? throw new ErrorServicio(401, "La sesión venció. Inicia sesión de nuevo.");

    /// <summary>
    /// Ejecuta la acción de un formulario y redirige (patrón POST-redirección-GET) con un mensaje
    /// para la página siguiente. Un 401 sube a la página de error, que cierra la sesión.
    /// </summary>
    protected async Task<IActionResult> Ejecutar(string destino, string exito, Func<Task> accion)
    {
        try
        {
            await accion();
            TempData["ok"] = exito;
        }
        catch (ErrorServicio e) when (e.Estado != 401)
        {
            TempData["error"] = e.ParaUsuario();
        }
        return Redirect(destino);
    }
}
