using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Portal.Servicios;

namespace Portal.Pages.Evaluaciones;

/// <summary>Evaluaciones de daño (mínimo d), filtradas por desastre. servidor es el dueño.</summary>
public class IndexModel(ServidorCliente servidor, NucleoCliente nucleo) : PaginaBase
{
    [BindProperty(SupportsGet = true)] public string? Desastre { get; set; }
    public JsonElement Evaluaciones { get; private set; }
    public JsonElement Desastres { get; private set; }

    public async Task OnGetAsync()
    {
        var token = Token;
        var evaluaciones = servidor.Evaluaciones(token, Desastre);
        var desastres = nucleo.Desastres(token);
        Evaluaciones = await evaluaciones;
        Desastres = await desastres;
    }
}
