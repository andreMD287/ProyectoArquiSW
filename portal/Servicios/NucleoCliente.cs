using System.Text.Json;

namespace Portal.Servicios;

/// <summary>
/// Núcleo (UD-02, Jakarta EE): identidad, sesión y desastres. Es el dueño del desastre y de su
/// fase, así que el portal lee y cambia los desastres directamente aquí, con el token del usuario.
/// </summary>
public sealed class NucleoCliente(HttpClient http)
{
    private readonly ClienteHttp cliente = new(http, "de identidad y desastres (núcleo)");

    public Task<JsonElement> Login(string usuario, string clave) =>
        cliente.Exigir(HttpMethod.Post, "auth/login", new { usuario, clave }, null);

    public Task Logout(string token) => cliente.Llamar(HttpMethod.Post, "auth/logout", new { }, token);

    public Task<JsonElement> CrearUsuario(string token, string usuario, string clave, string rol) =>
        cliente.Exigir(HttpMethod.Post, "auth/usuarios", new { usuario, clave, rol }, token);

    /// <summary>Desastres del organismo, con fase, nivel y ubicación.</summary>
    public Task<JsonElement> Desastres(string token) => cliente.Exigir(HttpMethod.Get, "eventos", null, token);

    public Task<JsonElement> Desastre(string token, string referencia) =>
        cliente.Exigir(HttpMethod.Get, "eventos/" + Uri.EscapeDataString(referencia), null, token);

    public Task<JsonElement> Historial(string token, string referencia) =>
        cliente.Exigir(HttpMethod.Get, "eventos/" + Uri.EscapeDataString(referencia) + "/historial", null, token);

    /// <summary>Registro del desastre (fase Registro). La referencia es idempotente.</summary>
    public Task<JsonElement> Registrar(string token, string referencia, string tipo, string ubicacion, string descripcion,
                                       string severidad, string fecha) =>
        cliente.Exigir(HttpMethod.Post, "integracion/registro",
            new { id = referencia, tipo, ubicacion, descripcion, severidad, fecha }, token);

    public Task<JsonElement> CambiarFase(string token, string referencia, long versionBase, string fase, string motivo) =>
        cliente.Exigir(HttpMethod.Patch, "eventos/" + Uri.EscapeDataString(referencia) + "/fase",
            new { cambioId = Guid.NewGuid().ToString(), versionBase, fase, motivo }, token);

    /// <summary>Contrato C2: ubicación y radio del área afectada, y nivel si cambia.</summary>
    public Task<JsonElement> Caracterizar(string token, string referencia, long versionBase, double latitud, double longitud,
                                          double radioKm, int? nivel, string? descripcion)
    {
        var cuerpo = new Dictionary<string, object?>
        {
            ["latitud"] = latitud, ["longitud"] = longitud, ["radioKm"] = radioKm,
            ["versionBase"] = versionBase, ["claveIdempotencia"] = Guid.NewGuid().ToString(),
        };
        if (nivel is not null) cuerpo["nivel"] = nivel;
        if (!string.IsNullOrWhiteSpace(descripcion)) cuerpo["descripcion"] = descripcion;
        return cliente.Exigir(HttpMethod.Patch, "eventos/" + Uri.EscapeDataString(referencia) + "/caracterizacion", cuerpo, token);
    }
}
