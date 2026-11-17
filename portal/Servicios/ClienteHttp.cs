using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Portal.Servicios;

public sealed record Respuesta(int Estado, JsonElement Cuerpo, byte[] Bytes, string? Tipo)
{
    public bool Exitosa => Estado is >= 200 and < 300;
}

/// <summary>Llamadas REST con el token del usuario; convierte los errores en ErrorServicio.</summary>
public sealed class ClienteHttp(HttpClient http, string nombre)
{
    public async Task<Respuesta> Llamar(HttpMethod metodo, string ruta, object? cuerpo, string? token)
    {
        using var peticion = new HttpRequestMessage(metodo, new Uri(http.BaseAddress!, ruta.TrimStart('/')));
        if (token is not null) peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null) peticion.Content = JsonContent.Create(cuerpo);
        try
        {
            using var r = await http.SendAsync(peticion);
            var bytes = await r.Content.ReadAsByteArrayAsync();
            return new Respuesta((int)r.StatusCode, Leer(bytes), bytes, r.Content.Headers.ContentType?.ToString());
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new ErrorServicio(503, $"El servicio {nombre} no responde en este momento.");
        }
    }

    /// <summary>Llama y exige una respuesta exitosa (2xx); si no, lanza ErrorServicio con el mensaje del servicio.</summary>
    public async Task<JsonElement> Exigir(HttpMethod metodo, string ruta, object? cuerpo, string? token)
    {
        var r = await Llamar(metodo, ruta, cuerpo, token);
        if (r.Exitosa) return r.Cuerpo;
        var regla = r.Cuerpo.Texto("regla");
        throw new ErrorServicio(r.Estado, Mensaje(r), regla.Length == 0 ? null : regla);
    }

    private string Mensaje(Respuesta r)
    {
        foreach (var campo in new[] { "mensaje", "detail", "title", "error" })
        {
            var texto = r.Cuerpo.Texto(campo);
            if (!string.IsNullOrWhiteSpace(texto)) return Traducir(texto);
        }
        if (r.Cuerpo.Tiene("errors")) return r.Cuerpo.GetProperty("errors").GetRawText();
        return r.Estado switch
        {
            401 => "La sesión venció. Inicia sesión de nuevo.",
            403 => "Tu rol no permite esta operación.",
            404 => "No se encontró el recurso.",
            409 => "Otro usuario cambió estos datos. Recarga la página y vuelve a intentarlo.",
            _ => $"El servicio {nombre} respondió {r.Estado}.",
        };
    }

    private static string Traducir(string codigo) => codigo switch
    {
        "SIN_PERMISO" => "Tu rol no permite esta operación.",
        "VERSION_DESACTUALIZADA" or "VERSION_O_CAMBIO_EN_CONFLICTO" =>
            "Otro usuario cambió el desastre. Recarga la página y vuelve a intentarlo.",
        "CREDENCIALES_INVALIDAS" => "Usuario o contraseña incorrectos.",
        "USUARIO_EXISTENTE" => "Ese usuario ya existe.",
        "DATOS_INVALIDOS" => "Revisa los datos: usuario de 3 a 80 caracteres y contraseña de al menos 12.",
        "DESASTRE_NO_ENCONTRADO" => "El desastre no existe en tu organismo.",
        _ => codigo,
    };

    private static JsonElement Leer(byte[] bytes)
    {
        if (bytes.Length == 0) return default;
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(bytes);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
