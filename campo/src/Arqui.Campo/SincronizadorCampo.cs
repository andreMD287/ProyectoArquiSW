using System.Text.Json;

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Serialization;

namespace Arqui.Campo;

internal static class SincronizadorCampo
{
    private static readonly HttpClient Cliente = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static async Task<HttpResponseMessage> EnviarConSesionAsync(HttpRequestMessage req)
    {
        SesionCampo.Autorizar(req);
        var response = await Cliente.SendAsync(req);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            SesionCampo.MarcarVencida();
            throw new SesionVencidaException();
        }
        return response;
    }

    public static bool TryCrearDestino(string? texto, [NotNullWhen(true)] out Uri? destino)
    {
        destino = null;
        if (!Uri.TryCreate(texto?.Trim(), UriKind.Absolute, out var servidor) ||
            !string.IsNullOrEmpty(servidor.UserInfo))
            return false;

        if (servidor.Scheme != Uri.UriSchemeHttps &&
            (servidor.Scheme != Uri.UriSchemeHttp || servidor.Host is not ("127.0.0.1" or "10.0.2.2")))
            return false;

        destino = new Uri(servidor, "/api/campo/lotes");
        return true;
    }

    public static async Task<CatalogoCampo> DescargarCatalogoAsync(AlmacenCampo almacen, Uri destino)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,new Uri(destino,"/api/desastres"));
        using var response = await EnviarConSesionAsync(req);
        response.EnsureSuccessStatusCode();
        var catalogo = await response.Content.ReadFromJsonAsync(CampoJson.Default.CatalogoCampo)
            ?? throw new InvalidDataException("Respuesta sin catálogo.");
        await almacen.GuardarCatalogoAsync(catalogo.Desastres.Select(r => r.Desastre));

        using var reqTipos = new HttpRequestMessage(HttpMethod.Get,new Uri(destino,"/api/catalogos"));
        using var respTipos = await EnviarConSesionAsync(reqTipos);
        respTipos.EnsureSuccessStatusCode();
        var tipos = await respTipos.Content.ReadFromJsonAsync(CampoJson.Default.CatalogoTiposActivo)
            ?? throw new InvalidDataException("Respuesta sin tipos de activo.");
        await almacen.GuardarTiposActivoAsync(tipos.TiposActivo);
        return catalogo;
    }

    public static async Task<int> EnviarAsync(AlmacenCampo almacen, Uri destino)
    {
        var pendientes = (await almacen.ListarAsync())
            .Where(r => r.NivelAfectacion is >= 1 and <= 5 && r.Estado is null or "recibida")
            .Reverse().Take(20).ToArray();

        // C2 primero: la primera caracterización puede activar la fase CARACTERIZACION.
        int procesadas = await EnviarCaracterizacionesAsync(almacen, destino);
        foreach (var lote in pendientes.Where(r => r.EvidenciaId is null).Chunk(20))
            procesadas += await EnviarLoteAsync(almacen, destino, lote);

        // Una foto por petición para mantenerse dentro del límite HTTP, incluso con base64.
        foreach (var registro in pendientes.Where(r => r.EvidenciaId is not null))
            procesadas += await EnviarLoteAsync(almacen, destino, [registro]);
        procesadas+=await EnviarRevisionesAsync(almacen,destino);
        return procesadas;
    }

    private static async Task<int> EnviarRevisionesAsync(AlmacenCampo almacen,Uri destino){
        int total=0;
        foreach(var r in (await almacen.RevisionesAsync()).Where(r=>r.Estado=="pendiente").Reverse()){
            using var req=new HttpRequestMessage(HttpMethod.Put,new Uri(destino,"/api/campo/evaluaciones/"+r.EvaluacionId));
            req.Content=JsonContent.Create(new RevisionPeticion(r.OperacionId,r.VersionBase,r.NivelAfectacion,r.Descripcion,r.Activo,r.Latitud,r.Longitud,r.Motivo),CampoJson.Default.RevisionPeticion);
            using var response=await EnviarConSesionAsync(req);
            if(response.StatusCode==System.Net.HttpStatusCode.Conflict){
                using var get=new HttpRequestMessage(HttpMethod.Get,req.RequestUri);using var actual=await EnviarConSesionAsync(get);actual.EnsureSuccessStatusCode();
                r.ServidorJson=await actual.Content.ReadAsStringAsync();r.Estado="conflicto";await almacen.ResultadoRevisionAsync(r);total++;continue;
            }
            response.EnsureSuccessStatusCode();var body=await response.Content.ReadFromJsonAsync(CampoJson.Default.RespuestaRevision) ?? throw new InvalidDataException();
            r.Estado="incorporada";await almacen.ResultadoRevisionAsync(r,body.Version);total++;
        }return total;
    }

    private static async Task<int> EnviarCaracterizacionesAsync(AlmacenCampo almacen, Uri destino)
    {
        int procesadas = 0;
        foreach (var c in (await almacen.CaracterizacionesAsync())
                     .Where(c => c.Estado == "pendiente").Reverse().Take(20))
        {
            var ruta = new Uri(destino, "/api/desastres/" + Uri.EscapeDataString(c.DesastreId) + "/caracterizacion");
            using var req = new HttpRequestMessage(HttpMethod.Patch, ruta);
            req.Content = JsonContent.Create(
                new PeticionCaracterizacion(c.Latitud,c.Longitud,c.RadioKm,c.Nivel,
                    c.Descripcion,c.VersionBase,c.ClaveIdempotencia),
                CampoJson.Default.PeticionCaracterizacion);
            using var respuesta = await EnviarConSesionAsync(req);
            var cuerpo = await respuesta.Content.ReadAsStringAsync();

            if (respuesta.IsSuccessStatusCode)
            {
                await almacen.ResultadoCaracterizacionAsync(c, "incorporada");
                procesadas++;
                continue;
            }
            if (respuesta.StatusCode == HttpStatusCode.Conflict)
            {
                string codigo = "CONFLICTO";
                long? version = null;
                try
                {
                    using var json = JsonDocument.Parse(cuerpo);
                    var raiz = json.RootElement;
                    if (raiz.TryGetProperty("error", out var e)) codigo = e.GetString() ?? codigo;
                    if (raiz.TryGetProperty("versionActual", out var v) && v.TryGetInt64(out long n)) version = n;
                }
                catch (JsonException) { /* conservar respuesta como conflicto, incluso sin JSON */ }
                await almacen.ResultadoCaracterizacionAsync(c,
                    codigo == "CLAVE_REUTILIZADA" ? "conflicto_clave" : "conflicto", codigo, version);
                procesadas++;
                continue;
            }
            if (respuesta.StatusCode == HttpStatusCode.Forbidden ||
                respuesta.StatusCode == HttpStatusCode.BadRequest ||
                respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                await almacen.ResultadoCaracterizacionAsync(c,
                    respuesta.StatusCode == HttpStatusCode.Forbidden ? "sin_permiso" : "rechazada",
                    $"HTTP {(int)respuesta.StatusCode}: " + cuerpo[..Math.Min(cuerpo.Length, 200)]);
                procesadas++;
                continue;
            }
            // 503, fallos transitorios y timeouts no cambian el registro pendiente.
            respuesta.EnsureSuccessStatusCode();
        }
        return procesadas;
    }

    private static async Task<int> EnviarLoteAsync(AlmacenCampo almacen, Uri destino, ResumenPendiente[] lote)
    {
        var operaciones = await Task.WhenAll(lote.Select(r => almacen.ObtenerAsync(r.EvaluacionId)));
        using var req=new HttpRequestMessage(HttpMethod.Post,destino);
        req.Content=JsonContent.Create(new LoteCampo(operaciones),CampoJson.Default.LoteCampo);
        using var respuesta = await EnviarConSesionAsync(req);
        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await respuesta.Content.ReadFromJsonAsync(CampoJson.Default.RespuestaCampo)
            ?? throw new InvalidDataException("Respuesta sin resultados.");
        foreach (var resultado in cuerpo.Resultados)
        {
            var operacion = operaciones.FirstOrDefault(op => op.OperacionId == resultado.OperacionId);
            if (operacion is not null && resultado.Estado is "incorporada" or "recibida" or "conflicto" or "rechazada")
                await almacen.ActualizarResultadoAsync(operacion.EvaluacionId, resultado.Estado, resultado.Motivo);
        }
        return cuerpo.Resultados.Length;
    }
}

internal sealed record LoteCampo(EvaluacionPendiente[] Operaciones);
internal sealed record RespuestaCampo(ResultadoCampo[] Resultados);
internal sealed record ResultadoCampo(string OperacionId, string Estado, string? Motivo);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(LoteCampo))]
[JsonSerializable(typeof(RespuestaCampo))]
[JsonSerializable(typeof(CatalogoCampo))]
[JsonSerializable(typeof(CredencialesCampo))]
[JsonSerializable(typeof(LoginCampo))]
[JsonSerializable(typeof(SesionLocal))]
[JsonSerializable(typeof(RevisionPeticion))]
[JsonSerializable(typeof(RespuestaRevision))]
[JsonSerializable(typeof(VersionCampo))]
[JsonSerializable(typeof(CatalogoTiposActivo))]
[JsonSerializable(typeof(PeticionCaracterizacion))]
internal partial class CampoJson : JsonSerializerContext { }

internal sealed record CatalogoCampo(bool Disponible, DateTimeOffset? ActualizadoEn, RegistroCatalogo[] Desastres);
internal sealed record RegistroCatalogo(DesastreLocal Desastre);

internal sealed record RevisionPeticion(string OperacionId,long VersionBase,int NivelAfectacion,string Descripcion,string? Activo,double Latitud,double Longitud,string Motivo);
internal sealed record RespuestaRevision(string Estado,long Version);
internal sealed record VersionCampo(long Version,int NivelAfectacion,string? Descripcion,string? Activo,double Latitud,double Longitud);
