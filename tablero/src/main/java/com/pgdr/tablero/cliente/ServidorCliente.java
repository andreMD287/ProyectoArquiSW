package com.pgdr.tablero.cliente;

import com.fasterxml.jackson.databind.JsonNode;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import org.springframework.http.HttpMethod;

/** servidor (UD-03): evaluaciones de daño de un desastre, con ubicación y nivel de afectación. */
public class ServidorCliente {
    private final ClienteHttp http;

    public ServidorCliente(ClienteHttp http) {
        this.http = http;
    }

    public JsonNode evaluaciones(String token, String ref) {
        return http.exigir(HttpMethod.GET, "/api/panorama?clase=EVALUACION&eventoId="
                + URLEncoder.encode(ref, StandardCharsets.UTF_8), null, token);
    }
}
