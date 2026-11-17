package com.pgdr.tablero.cliente;

import com.fasterxml.jackson.databind.JsonNode;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import org.springframework.http.HttpMethod;

/** Núcleo (UD-02): dueño del desastre y de su fase. El tablero solo lee el desastre. */
public class NucleoCliente {
    private final ClienteHttp http;

    public NucleoCliente(ClienteHttp http) {
        this.http = http;
    }

    public JsonNode desastre(String token, String ref) {
        return http.exigir(HttpMethod.GET, "/eventos/" + URLEncoder.encode(ref, StandardCharsets.UTF_8), null, token);
    }
}
