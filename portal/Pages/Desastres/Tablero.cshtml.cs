using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Portal.Servicios;

namespace Portal.Pages.Desastres;

/// <summary>
/// Tablero de un desastre: sus datos e historial (núcleo) aquí; el mapa y los resúmenes los trae el
/// navegador desde /tablero (servicio de tablero, servlet multihilo). Desde aquí también se cambia
/// la fase y se caracteriza el desastre (contrato C2).
/// </summary>
public class TableroModel(NucleoCliente nucleo) : PaginaBase
{
    [BindProperty(SupportsGet = true)] public string Referencia { get; set; } = "";
    public JsonElement Desastre { get; private set; }
    public JsonElement Historial { get; private set; }
    public IReadOnlyList<string> Siguientes { get; private set; } = [];
    public string Fase => Desastre.Texto("fase", "REGISTRO");

    public async Task OnGetAsync()
    {
        var token = Token;
        var desastre = nucleo.Desastre(token, Referencia);
        var historial = nucleo.Historial(token, Referencia);
        Desastre = await desastre;
        Historial = await historial;
        Siguientes = SiguientesFases(Fase);
    }

    public Task<IActionResult> OnPostFaseAsync(long versionBase, string fase, string motivo) =>
        Ejecutar(Destino, $"La fase cambió a {fase}.", () => nucleo.CambiarFase(Token, Referencia, versionBase, fase, motivo));

    public Task<IActionResult> OnPostCaracterizacionAsync(long versionBase, double latitud, double longitud, double radioKm,
                                                          int? nivel, string? descripcion) =>
        Ejecutar(Destino, "Caracterización guardada.",
            () => nucleo.Caracterizar(Token, Referencia, versionBase, latitud, longitud, radioKm, nivel, descripcion));

    private string Destino => "/desastres/" + Referencia;

    /// <summary>Fases a las que el coordinador puede llevar el desastre desde aquí (Reconstrucción se inicia en su módulo).</summary>
    public static IReadOnlyList<string> SiguientesFases(string fase) => fase switch
    {
        "REGISTRO" => ["CARACTERIZACION", "CIERRE"],
        "CARACTERIZACION" => ["CIERRE"],
        _ => [],
    };
}
