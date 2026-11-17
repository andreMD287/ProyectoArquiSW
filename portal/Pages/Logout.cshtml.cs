using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Servicios;
using Portal.Sesion;

namespace Portal.Pages;

/// <summary>Cierre de sesión: revoca el token también en el núcleo y borra la cookie.</summary>
public class LogoutModel(NucleoCliente nucleo, AlmacenSesiones almacen) : PageModel
{
    public IActionResult OnGet() => Redirect("/desastres");

    public async Task<IActionResult> OnPostAsync()
    {
        var sesion = SesionUsuario.De(User);
        if (sesion is not null)
        {
            var token = almacen.Token(sesion.SesionId);
            if (token is not null)
            {
                try
                {
                    await nucleo.Logout(token);
                }
                catch (ErrorServicio)
                {
                    // Si el núcleo no responde, la sesión del portal se cierra igual.
                }
            }
            almacen.Quitar(sesion.SesionId);
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login?salida");
    }
}
