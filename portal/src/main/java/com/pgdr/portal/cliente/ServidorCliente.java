package com.pgdr.portal.cliente;

import com.fasterxml.jackson.databind.JsonNode;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import org.springframework.http.HttpMethod;

/** servidor (UD-03): evaluaciones de daño capturadas en campo, su historial y sus fotos. */
public class ServidorCliente {
    private final ClienteHttp http;

    public ServidorCliente(ClienteHttp http) {
        this.http = http;
    }

    /** Evaluaciones de un desastre (o de todos con ref nula), con ubicación y nivel de afectación. */
    public JsonNode evaluaciones(String token, String ref) {
        String filtro = ref == null || ref.isBlank() ? "" : "&eventoId=" + URLEncoder.encode(ref, StandardCharsets.UTF_8);
        return http.exigir(HttpMethod.GET, "/api/panorama?clase=EVALUACION" + filtro, null, token);
    }

    public JsonNode evaluacion(String token, UUID id) {
        return http.exigir(HttpMethod.GET, "/api/campo/evaluaciones/" + id, null, token);
    }

    public JsonNode historial(String token, UUID id) {
        return http.exigir(HttpMethod.GET, "/api/campo/evaluaciones/" + id + "/historial", null, token);
    }

    public ClienteHttp.Respuesta foto(String token, UUID id) {
        return http.llamar(HttpMethod.GET, "/api/campo/evaluaciones/" + id + "/foto", null, token);
    }
}
