using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Portal.Servicios;

namespace Portal.Pages.Desastres;

/// <summary>
/// Registro de un desastre (nace en fase Registro). Las propiedades con [BindProperty] son el modelo
/// de la vista: el formulario las llena y las anotaciones las validan antes de llamar al núcleo.
/// </summary>
public class NuevoModel(NucleoCliente nucleo) : PaginaBase
{
    public static readonly string[] Tipos =
        ["TERREMOTO", "INUNDACION", "DESLIZAMIENTO", "INCENDIO_FORESTAL", "HURACAN", "ERUPCION_VOLCANICA", "SEQUIA", "OTRO"];

    [BindProperty, Required(ErrorMessage = "Elige el tipo de desastre.")]
    public string Tipo { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Escribe la ubicación."), StringLength(200)]
    public string Ubicacion { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Escribe una descripción inicial."), StringLength(2000)]
    public string Descripcion { get; set; } = "";

    [BindProperty, Required, RegularExpression("BAJA|MEDIA|ALTA|CRITICA", ErrorMessage = "Nivel desconocido.")]
    public string Severidad { get; set; } = "MEDIA";

    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        // Referencia legible y única; si el registro se repite, el núcleo lo trata como el mismo.
        var referencia = $"DES-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        try
        {
            await nucleo.Registrar(Token, referencia, Tipo, Ubicacion.Trim(), Descripcion.Trim(), Severidad,
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        }
        catch (ErrorServicio e) when (e.Estado != 401)
        {
            Error = e.ParaUsuario();
            return Page();
        }
        TempData["ok"] = "Desastre registrado en fase Registro.";
        return Redirect("/desastres/" + referencia);
    }
}
