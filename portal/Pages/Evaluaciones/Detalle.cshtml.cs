using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Portal.Servicios;

namespace Portal.Pages.Evaluaciones;

/// <summary>Detalle de una evaluación con su historial de versiones y su foto.</summary>
public class DetalleModel(ServidorCliente servidor) : PaginaBase
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    public JsonElement Evaluacion { get; private set; }
    public JsonElement Historial { get; private set; }

    public async Task OnGetAsync()
    {
        var token = Token;
        var evaluacion = servidor.Evaluacion(token, Id);
        var historial = servidor.Historial(token, Id);
        Evaluacion = await evaluacion;
        Historial = await historial;
    }

    /// <summary>La foto pasa por el portal con el token del usuario; el navegador nunca ve el token.</summary>
    public async Task<IActionResult> OnGetFotoAsync()
    {
        var r = await servidor.Foto(Token, Id);
        if (r.Estado != 200) return NotFound();
        Response.Headers.CacheControl = "private, max-age=300";
        return File(r.Bytes, r.Tipo ?? "application/octet-stream");
    }
}
