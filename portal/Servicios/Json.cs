using System.Text.Json;

namespace Portal.Servicios;

/// <summary>
/// Lectura tolerante de las respuestas JSON de los servicios: un campo ausente o nulo da el valor
/// por defecto en vez de una excepción (las vistas muestran "—" o "Sin vincular").
/// </summary>
public static class Json
{
    public static bool Tiene(this JsonElement e, string campo) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(campo, out var v)
        && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

    public static JsonElement Obj(this JsonElement e, string campo) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(campo, out var v) ? v : default;

    /// <summary>El campo como texto; si falta o es nulo, el valor por defecto.</summary>
    public static string Texto(this JsonElement e, string campo, string defecto = "")
    {
        if (!e.Tiene(campo)) return defecto;
        var v = e.GetProperty(campo);
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? defecto : v.GetRawText();
    }

    public static int Entero(this JsonElement e, string campo, int defecto = 0) =>
        e.Tiene(campo) && e.GetProperty(campo).ValueKind == JsonValueKind.Number && e.GetProperty(campo).TryGetInt32(out var n)
            ? n : defecto;

    public static long Largo(this JsonElement e, string campo, long defecto = 0) =>
        e.Tiene(campo) && e.GetProperty(campo).ValueKind == JsonValueKind.Number && e.GetProperty(campo).TryGetInt64(out var n)
            ? n : defecto;

    public static double Numero(this JsonElement e, string campo, double defecto = 0) =>
        e.Tiene(campo) && e.GetProperty(campo).ValueKind == JsonValueKind.Number ? e.GetProperty(campo).GetDouble() : defecto;

    /// <summary>El número tal como viene en el JSON (con punto decimal), o vacío; sirve para atributos y campos de formulario.</summary>
    public static string NumeroCrudo(this JsonElement e, string campo) =>
        e.Tiene(campo) && e.GetProperty(campo).ValueKind == JsonValueKind.Number ? e.GetProperty(campo).GetRawText() : "";

    public static IEnumerable<JsonElement> Elementos(this JsonElement e) =>
        e.ValueKind == JsonValueKind.Array ? e.EnumerateArray() : Enumerable.Empty<JsonElement>();
}
