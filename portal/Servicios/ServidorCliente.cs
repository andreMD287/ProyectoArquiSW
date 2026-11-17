using System.Text.Json;

namespace Portal.Servicios;

/// <summary>servidor (UD-03, ASP.NET Core): evaluaciones de daño capturadas en campo, su historial y sus fotos.</summary>
public sealed class ServidorCliente(HttpClient http)
{
    private readonly ClienteHttp cliente = new(http, "de evaluaciones (servidor)");

    /// <summary>Evaluaciones de un desastre (o de todos, sin referencia), con ubicación y nivel de afectación.</summary>
    public Task<JsonElement> Evaluaciones(string token, string? referencia)
    {
        var filtro = string.IsNullOrWhiteSpace(referencia) ? "" : "&eventoId=" + Uri.EscapeDataString(referencia);
        return cliente.Exigir(HttpMethod.Get, "api/panorama?clase=EVALUACION" + filtro, null, token);
    }

    public Task<JsonElement> Evaluacion(string token, Guid id) =>
        cliente.Exigir(HttpMethod.Get, $"api/campo/evaluaciones/{id}", null, token);

    public Task<JsonElement> Historial(string token, Guid id) =>
        cliente.Exigir(HttpMethod.Get, $"api/campo/evaluaciones/{id}/historial", null, token);

    public Task<Respuesta> Foto(string token, Guid id) =>
        cliente.Llamar(HttpMethod.Get, $"api/campo/evaluaciones/{id}/foto", null, token);
}
