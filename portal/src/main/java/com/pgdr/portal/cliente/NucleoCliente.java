package com.pgdr.portal.cliente;

import com.fasterxml.jackson.databind.JsonNode;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;
import org.springframework.http.HttpMethod;

/**
 * Núcleo (UD-02): identidad, sesión y desastres. Es el dueño del desastre y de su fase, así que
 * el portal lee y cambia los desastres directamente aquí, con el token del usuario.
 */
public class NucleoCliente {
    private final ClienteHttp http;

    public NucleoCliente(ClienteHttp http) {
        this.http = http;
    }

    public JsonNode login(String usuario, String clave) {
        return http.exigir(HttpMethod.POST, "/auth/login", Map.of("usuario", usuario, "clave", clave), null);
    }

    public void logout(String token) {
        http.llamar(HttpMethod.POST, "/auth/logout", Map.of(), token);
    }

    public JsonNode crearUsuario(String token, String usuario, String clave, String rol) {
        return http.exigir(HttpMethod.POST, "/auth/usuarios", Map.of("usuario", usuario, "clave", clave, "rol", rol), token);
    }

    /** Desastres del organismo, con fase, nivel y ubicación. */
    public JsonNode desastres(String token) {
        return http.exigir(HttpMethod.GET, "/eventos", null, token);
    }

    public JsonNode desastre(String token, String ref) {
        return http.exigir(HttpMethod.GET, "/eventos/" + ruta(ref), null, token);
    }

    public JsonNode historial(String token, String ref) {
        return http.exigir(HttpMethod.GET, "/eventos/" + ruta(ref) + "/historial", null, token);
    }

    /** Registro del desastre (fase Registro). La referencia es idempotente. */
    public JsonNode registrar(String token, String ref, String tipo, String ubicacion, String descripcion,
                              String severidad, String fecha) {
        Map<String, Object> cuerpo = new LinkedHashMap<>();
        cuerpo.put("id", ref);
        cuerpo.put("tipo", tipo);
        cuerpo.put("ubicacion", ubicacion);
        cuerpo.put("descripcion", descripcion);
        cuerpo.put("severidad", severidad);
        cuerpo.put("fecha", fecha);
        return http.exigir(HttpMethod.POST, "/integracion/registro", cuerpo, token);
    }

    public JsonNode cambiarFase(String token, String ref, long versionBase, String fase, String motivo) {
        return http.exigir(HttpMethod.PATCH, "/eventos/" + ruta(ref) + "/fase", Map.of("cambioId", UUID.randomUUID().toString(),
                "versionBase", versionBase, "fase", fase, "motivo", motivo), token);
    }

    /** Contrato C2. */
    public JsonNode caracterizar(String token, String ref, long versionBase, double latitud, double longitud,
                                 double radioKm, Integer nivel, String descripcion) {
        Map<String, Object> cuerpo = new LinkedHashMap<>();
        cuerpo.put("latitud", latitud);
        cuerpo.put("longitud", longitud);
        cuerpo.put("radioKm", radioKm);
        if (nivel != null) cuerpo.put("nivel", nivel);
        if (descripcion != null && !descripcion.isBlank()) cuerpo.put("descripcion", descripcion);
        cuerpo.put("versionBase", versionBase);
        cuerpo.put("claveIdempotencia", UUID.randomUUID().toString());
        return http.exigir(HttpMethod.PATCH, "/eventos/" + ruta(ref) + "/caracterizacion", cuerpo, token);
    }

    private static String ruta(String ref) {
        return URLEncoder.encode(ref, StandardCharsets.UTF_8);
    }
}
