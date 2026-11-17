namespace Portal.Servicios;

/// <summary>
/// Servicio de tablero (UD-10, Java): su servlet asíncrono multihilo consulta en paralelo el
/// núcleo, servidor y Reconstrucción y devuelve el tablero de un desastre en JSON (ADR-24).
/// </summary>
public sealed class TableroCliente(HttpClient http)
{
    private readonly ClienteHttp cliente = new(http, "de tablero");

    public Task<Respuesta> Consultar(string token, string referencia, string? modo) =>
        cliente.Llamar(HttpMethod.Get, "tablero?desastre=" + Uri.EscapeDataString(referencia)
            + (modo == "secuencial" ? "&modo=secuencial" : ""), null, token);
}
