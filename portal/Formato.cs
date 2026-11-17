using System.Globalization;

namespace Portal;

/// <summary>Formatos para las vistas (Colombia): pesos con punto de miles y decimales con coma.</summary>
public static class Formato
{
    private static readonly NumberFormatInfo Colombia = new()
    {
        NumberGroupSeparator = ".", NumberDecimalSeparator = ",", NumberGroupSizes = [3],
    };

    /// <summary>"1000000.00" → "$ 1.000.000". Vacío o inválido → "—".</summary>
    public static string Dinero(string? valor) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
            ? "$ " + n.ToString("#,0.##", Colombia) : "—";

    /// <summary>"3.0" con 1 decimal → "3,0".</summary>
    public static string Decimal(string? valor, int decimales) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
            ? n.ToString(decimales > 0 ? "#,0." + new string('0', decimales) : "#,0", Colombia) : "—";

    public static string Fase(string? fase) => fase switch
    {
        "REGISTRO" => "Registro",
        "CARACTERIZACION" => "Caracterización",
        "RECONSTRUCCION" => "Reconstrucción",
        "CIERRE" => "Cierre",
        _ => fase ?? "",
    };

    /// <summary>"INCENDIO_FORESTAL" → "Incendio forestal".</summary>
    public static string Nombre(string codigo)
    {
        var texto = codigo.Replace('_', ' ').ToLowerInvariant();
        return texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
    }

    /// <summary>Fecha ISO recortada a minutos: "2026-11-15T12:05:00Z" → "2026-11-15T12:05".</summary>
    public static string Corta(string? fecha) => fecha is null ? "" : fecha.Length > 16 ? fecha[..16] : fecha;

    /// <summary>"" → "—".</summary>
    public static string OGuion(string? texto) => string.IsNullOrEmpty(texto) ? "—" : texto;
}
