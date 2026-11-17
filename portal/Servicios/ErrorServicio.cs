namespace Portal.Servicios;

/// <summary>
/// Respuesta de error de un servicio (núcleo, servidor, Reconstrucción o tablero), con el estado
/// HTTP equivalente, el mensaje para el usuario y la regla de negocio si la hay (RN-xx).
/// </summary>
public sealed class ErrorServicio(int estado, string mensaje, string? regla = null) : Exception(mensaje)
{
    public int Estado { get; } = estado;
    public string? Regla { get; } = regla;

    /// <summary>Mensaje para mostrar en la página, con la regla si la hay.</summary>
    public string ParaUsuario() => (string.IsNullOrWhiteSpace(Regla) ? "" : Regla + ": ") + Message;
}
