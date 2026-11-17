using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Portal.Servicios;

namespace Portal.Pages;

/// <summary>Usuarios del organismo (solo COORDINADOR). El núcleo crea el usuario en el organismo de quien lo pide.</summary>
public class UsuariosModel(NucleoCliente nucleo) : PaginaBase
{
    [BindProperty, Required(ErrorMessage = "Escribe el usuario."),
     RegularExpression("[a-z0-9_.\\-]{3,80}", ErrorMessage = "El usuario lleva de 3 a 80 letras minúsculas, números, punto, guion o guion bajo.")]
    public string Usuario { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Escribe la contraseña."),
     StringLength(256, MinimumLength = 12, ErrorMessage = "La contraseña debe tener al menos 12 caracteres.")]
    public string Clave { get; set; } = "";

    [BindProperty, Required, RegularExpression("EVALUADOR|CONSULTA|COORDINADOR", ErrorMessage = "Rol desconocido.")]
    public string Rol { get; set; } = "EVALUADOR";

    public void OnGet() => ExigirCoordinador();

    public async Task<IActionResult> OnPostAsync()
    {
        ExigirCoordinador();
        if (!ModelState.IsValid) return Page();
        var nombre = Usuario.Trim().ToLowerInvariant();
        return await Ejecutar("/usuarios", $"Usuario {nombre} creado con rol {Rol}.",
            () => nucleo.CrearUsuario(Token, Usuario.Trim(), Clave, Rol));
    }

    private void ExigirCoordinador()
    {
        if (!Sesion.EsCoordinador) throw new ErrorServicio(403, "Solo un coordinador administra los usuarios del organismo.");
    }
}
