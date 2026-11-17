using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Servicios;
using Portal.Sesion;

namespace Portal.Pages;

/// <summary>Inicio de sesión. La identidad es la del núcleo: el portal no guarda contraseñas.</summary>
public class LoginModel(NucleoCliente nucleo, AlmacenSesiones almacen) : PageModel
{
    [BindProperty] public string? Usuario { get; set; }
    [BindProperty] public string? Clave { get; set; }
    [BindProperty(SupportsGet = true)] public string? Siguiente { get; set; }
    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrEmpty(Clave))
        {
            Error = "Escribe el usuario y la contraseña.";
            return Page();
        }
        try
        {
            var r = await nucleo.Login(Usuario.Trim(), Clave);
            var expira = DateTimeOffset.Parse(r.Texto("expiraEn"), CultureInfo.InvariantCulture);
            var anterior = SesionUsuario.De(User);
            if (anterior is not null) almacen.Quitar(anterior.SesionId);
            var sesion = new SesionUsuario(r.Texto("usuario"), r.Texto("rol"), r.Texto("organizacionId"),
                almacen.Guardar(r.Texto("accessToken"), expira));
            // Cookie nueva en cada inicio de sesión (evita la fijación de sesión); lleva el id, no el token.
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, sesion.ComoPrincipal(),
                new AuthenticationProperties { ExpiresUtc = expira, IsPersistent = false });
            return LocalRedirect(Url.IsLocalUrl(Siguiente) ? Siguiente! : "/desastres");
        }
        catch (ErrorServicio e)
        {
            Error = e.Estado == 401 ? "Usuario o contraseña incorrectos." : e.ParaUsuario();
            return Page();
        }
    }
}
