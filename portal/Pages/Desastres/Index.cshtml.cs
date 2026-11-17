using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Portal.Servicios;

namespace Portal.Pages.Desastres;

/// <summary>Desastres del organismo, con el conteo por fase y el filtro por fase. El núcleo es el dueño.</summary>
public class IndexModel(NucleoCliente nucleo) : PaginaBase
{
    public static readonly string[] Fases = ["REGISTRO", "CARACTERIZACION", "RECONSTRUCCION", "CIERRE"];

    [BindProperty(SupportsGet = true)] public string? Fase { get; set; }
    public List<JsonElement> Desastres { get; } = [];
    public Dictionary<string, int> PorFase { get; } = Fases.ToDictionary(f => f, _ => 0);
    public int Total { get; private set; }

    public async Task OnGetAsync()
    {
        foreach (var d in (await nucleo.Desastres(Token)).Elementos())
        {
            var fase = d.Texto("fase", "REGISTRO");
            PorFase[fase] = PorFase.GetValueOrDefault(fase) + 1;
            Total++;
            if (string.IsNullOrEmpty(Fase) || Fase == fase) Desastres.Add(d);
        }
    }
}
