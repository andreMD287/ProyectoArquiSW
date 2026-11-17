using SQLite;
using System.Text.Json.Serialization;

namespace Arqui.Campo;

// La misma clave se conserva en los reintentos de la MISMA captura.
public sealed class CaracterizacionPendiente
{
    [PrimaryKey]
    [JsonIgnore]
    public string ClaveIdempotencia { get; set; } = Guid.NewGuid().ToString();
    [JsonIgnore] public string OrganizacionLocal { get; set; } = "";
    [JsonIgnore] public string UsuarioLocal { get; set; } = "";
    public string DesastreId { get; set; } = "";
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public double RadioKm { get; set; }
    public int? Nivel { get; set; }
    public string Descripcion { get; set; } = "";
    public long VersionBase { get; set; }
    [JsonIgnore] public string Estado { get; set; } = "pendiente";
    [JsonIgnore] public string? Motivo { get; set; }
    [JsonIgnore] public long? VersionActual { get; set; }
    [JsonIgnore] public string CapturadaEnUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
}

public sealed class TipoActivoLocal
{
    [PrimaryKey] public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}

// DTO exacto C2: no enviar los campos de seguimiento del registro local.
internal sealed record PeticionCaracterizacion(
    double Latitud, double Longitud, double RadioKm, int? Nivel,
    string Descripcion, long VersionBase, string ClaveIdempotencia);

internal sealed record CatalogoTiposActivo(TipoActivoLocal[] TiposActivo);

internal sealed class SesionVencidaException : Exception
{
    public SesionVencidaException() : base("La sesión venció. Inicia sesión de nuevo; tus capturas siguen guardadas.") { }
}
