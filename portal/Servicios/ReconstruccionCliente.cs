using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Grpc.Core;
using Grpc.Net.Client;
using Pgdr.Reconstruccion.V1;

namespace Portal.Servicios;

/// <summary>
/// Reconstrucción (UD-06, Spring Boot) por gRPC. El cliente se genera del mismo contrato
/// reconstruccion.proto que implementa el servicio Java: los servicios comparten el contrato, no
/// los tipos. El token del usuario viaja en el metadato "authorization" y Reconstrucción lo valida
/// contra el núcleo.
/// </summary>
public sealed partial class ReconstruccionCliente(GrpcChannel canal)
{
    private const int PlazoMs = 5000;
    private readonly ReconstruccionService.ReconstruccionServiceClient servicio = new(canal);

    /// <summary>
    /// Reconstrucción identifica el desastre con un UUID derivado de su referencia en el núcleo:
    /// el mismo que da UUID.nameUUIDFromBytes("desastre:" + referencia) en Java (MD5, versión 3).
    /// </summary>
    public static string DesastreId(string referencia)
    {
        var h = MD5.HashData(Encoding.UTF8.GetBytes("desastre:" + referencia));
        h[6] = (byte)((h[6] & 0x0f) | 0x30);
        h[8] = (byte)((h[8] & 0x3f) | 0x80);
        var x = Convert.ToHexString(h).ToLowerInvariant();
        return $"{x[..8]}-{x[8..12]}-{x[12..16]}-{x[16..20]}-{x[20..]}";
    }

    public Task<SeguimientoResponse> Seguimiento(string token, string referencia) =>
        Llamar(token, o => servicio.ConsultarSeguimientoAsync(Desastre(referencia), o));

    public Task<EvaluacionesResponse> Evaluaciones(string token, string referencia) =>
        Llamar(token, o => servicio.ConsultarEvaluacionesAsync(Desastre(referencia), o));

    public Task<DesastreMsg> IniciarReconstruccion(string token, string referencia) =>
        Llamar(token, o => servicio.IniciarReconstruccionAsync(Desastre(referencia), o));

    public Task<DesastreMsg> Cerrar(string token, string referencia) =>
        Llamar(token, o => servicio.CerrarDesastreAsync(Desastre(referencia), o));

    public Task<ProyectoMsg> Crear(string token, string referencia, string nombre, string? tipo, IEnumerable<string> evaluaciones)
    {
        var proyecto = new NuevoProyecto { Nombre = nombre, Tipo = tipo ?? "" };
        proyecto.Evaluaciones.AddRange(evaluaciones);
        return Llamar(token, o => servicio.CrearProyectoAsync(
            new CrearProyectoRequest { DesastreId = DesastreId(referencia), Proyecto = proyecto }, o));
    }

    public Task<ProyectoMsg> Aprobar(string token, string id, string? presupuesto, string? fuente, string? responsable, long? version)
    {
        var q = new AprobarProyectoRequest
        {
            ProyectoId = id, Presupuesto = presupuesto ?? "", FuenteFinanciacion = fuente ?? "", Responsable = responsable ?? "",
        };
        if (version is long v) q.VersionEsperada = v;
        return Llamar(token, o => servicio.AprobarProyectoAsync(q, o));
    }

    public Task<ProyectoMsg> IniciarEjecucion(string token, string id, string? contratista, string? inicio, string? fin, long? version)
    {
        var q = new IniciarEjecucionRequest { ProyectoId = id, Contratista = contratista ?? "", Inicio = inicio ?? "", Fin = fin ?? "" };
        if (version is long v) q.VersionEsperada = v;
        return Llamar(token, o => servicio.IniciarEjecucionAsync(q, o));
    }

    public Task<ProyectoMsg> Avance(string token, string id, int porcentaje, string? descripcion, string? motivo, long? version)
    {
        var q = new RegistrarAvanceRequest
        {
            ProyectoId = id, Porcentaje = porcentaje, Descripcion = descripcion ?? "", MotivoCorreccion = motivo ?? "",
        };
        if (version is long v) q.VersionEsperada = v;
        return Llamar(token, o => servicio.RegistrarAvanceAsync(q, o));
    }

    public Task<ProyectoMsg> Costo(string token, string id, string? valor, string? descripcion, long? version)
    {
        var q = new RegistrarCostoRequest { ProyectoId = id, Valor = valor ?? "", Descripcion = descripcion ?? "" };
        if (version is long v) q.VersionEsperada = v;
        return Llamar(token, o => servicio.RegistrarCostoAsync(q, o));
    }

    public Task<ProyectoMsg> Evidencia(string token, string id, string? descripcion, long? version)
    {
        var q = new AdjuntarEvidenciaRequest { ProyectoId = id, Descripcion = descripcion ?? "" };
        if (version is long v) q.VersionEsperada = v;
        return Llamar(token, o => servicio.AdjuntarEvidenciaAsync(q, o));
    }

    public Task<ProyectoMsg> Presupuesto(string token, string id, string? valor, string? motivo, long? version)
    {
        var q = new ModificarPresupuestoRequest { ProyectoId = id, Presupuesto = valor ?? "", Motivo = motivo ?? "" };
        if (version is long v) q.VersionEsperada = v;
        return Llamar(token, o => servicio.ModificarPresupuestoAsync(q, o));
    }

    public Task<ProyectoMsg> Estado(string token, string id, string? estado, string? motivo, long? version)
    {
        var q = new CambiarEstadoRequest { ProyectoId = id, Estado = estado ?? "", Motivo = motivo ?? "" };
        if (version is long v) q.VersionEsperada = v;
        return Llamar(token, o => servicio.CambiarEstadoAsync(q, o));
    }

    public Task<AuditoriaResponse> Auditoria(string token, string id) =>
        Llamar(token, o => servicio.ConsultarAuditoriaAsync(new ConsultarAuditoriaRequest { ProyectoId = id }, o));

    private static DesastreRequest Desastre(string referencia) => new() { DesastreId = DesastreId(referencia) };

    private static async Task<T> Llamar<T>(string token, Func<CallOptions, AsyncUnaryCall<T>> rpc)
    {
        var opciones = new CallOptions(new Metadata { { "authorization", "Bearer " + token } },
            DateTime.UtcNow.AddMilliseconds(PlazoMs));
        try
        {
            return await rpc(opciones);
        }
        catch (RpcException e)
        {
            throw Traducir(e);
        }
    }

    /// <summary>Los códigos gRPC de Reconstrucción, como errores del portal (la regla RN-xx aparte).</summary>
    public static ErrorServicio Traducir(RpcException e)
    {
        var descripcion = e.Status.Detail ?? "";
        string? regla = null;
        var mensaje = descripcion;
        var m = Regla().Match(descripcion);
        if (m.Success)
        {
            regla = m.Groups[1].Value;
            mensaje = descripcion[(descripcion.IndexOf(':') + 1)..].Trim();
        }
        var estado = e.StatusCode switch
        {
            StatusCode.FailedPrecondition => 422,
            StatusCode.PermissionDenied => 403,
            StatusCode.Unauthenticated => 401,
            StatusCode.NotFound => 404,
            StatusCode.Aborted => 409,
            StatusCode.InvalidArgument => 400,
            StatusCode.DeadlineExceeded or StatusCode.Unavailable => 503,
            _ => 500,
        };
        if (string.IsNullOrWhiteSpace(mensaje))
            mensaje = estado == 503 ? "Reconstrucción no responde en este momento." : $"Reconstrucción respondió {e.StatusCode}.";
        return new ErrorServicio(estado, mensaje, regla);
    }

    [GeneratedRegex(@"^(RN-\d+):")]
    private static partial Regex Regla();
}
