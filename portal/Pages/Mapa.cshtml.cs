using System.Text.Json;
using Portal.Servicios;

namespace Portal.Pages;

/// <summary>
/// Mapa general (mínimo c): todos los desastres del organismo con su área afectada y su fase. El
/// mapa de un desastre, con evaluaciones y proyectos, está en su tablero.
/// </summary>
public class MapaModel(NucleoCliente nucleo) : PaginaBase
{
    /// <summary>Los desastres como JSON para el script del mapa (codificado para ir dentro de &lt;script&gt;).</summary>
    public string DesastresJson { get; private set; } = "[]";

    public async Task OnGetAsync() => DesastresJson = JsonSerializer.Serialize(await nucleo.Desastres(Token));
}
