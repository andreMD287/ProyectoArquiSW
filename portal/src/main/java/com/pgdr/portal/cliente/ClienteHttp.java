package com.pgdr.portal.cliente;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import org.springframework.http.HttpMethod;
import org.springframework.http.MediaType;
import org.springframework.web.client.ResourceAccessException;
import org.springframework.web.client.RestClient;

/** Llamadas REST con el token del usuario; convierte los errores en ErrorServicio. */
public class ClienteHttp {
    public record Respuesta(int estado, JsonNode cuerpo, byte[] bytes, String tipo) {}

    private final RestClient cliente;
    private final ObjectMapper json;
    private final String nombre;

    public ClienteHttp(RestClient cliente, ObjectMapper json, String nombre) {
        this.cliente = cliente;
        this.json = json;
        this.nombre = nombre;
    }

    public Respuesta llamar(HttpMethod metodo, String uri, Object cuerpo, String token) {
        try {
            RestClient.RequestBodySpec peticion = cliente.method(metodo).uri(uri);
            if (token != null) peticion.header("Authorization", "Bearer " + token);
            if (cuerpo != null) peticion.contentType(MediaType.APPLICATION_JSON).body(cuerpo);
            return peticion.exchange((req, res) -> {
                byte[] bytes = res.getBody().readAllBytes();
                String tipo = res.getHeaders().getContentType() == null ? null : res.getHeaders().getContentType().toString();
                return new Respuesta(res.getStatusCode().value(), leer(bytes), bytes, tipo);
            });
        } catch (ResourceAccessException e) {
            throw new ErrorServicio(503, "El servicio " + nombre + " no responde en este momento.", null);
        }
    }

    /** Llama y exige una respuesta exitosa (2xx); si no, lanza ErrorServicio con el mensaje del servicio. */
    public JsonNode exigir(HttpMethod metodo, String uri, Object cuerpo, String token) {
        Respuesta r = llamar(metodo, uri, cuerpo, token);
        if (r.estado() >= 200 && r.estado() < 300) return r.cuerpo();
        throw new ErrorServicio(r.estado(), mensaje(r), r.cuerpo() == null ? null : r.cuerpo().path("regla").asText(null));
    }

    private String mensaje(Respuesta r) {
        JsonNode c = r.cuerpo();
        if (c != null) {
            for (String campo : new String[] {"mensaje", "detail", "title", "error"}) {
                String texto = c.path(campo).asText("");
                if (!texto.isBlank()) return traducir(texto);
            }
            if (c.has("errors")) return c.get("errors").toString();
        }
        return switch (r.estado()) {
            case 401 -> "La sesión venció. Inicia sesión de nuevo.";
            case 403 -> "Tu rol no permite esta operación.";
            case 404 -> "No se encontró el recurso.";
            case 409 -> "Otro usuario cambió estos datos. Recarga la página y vuelve a intentarlo.";
            default -> "El servicio " + nombre + " respondió " + r.estado() + ".";
        };
    }

    private static String traducir(String codigo) {
        return switch (codigo) {
            case "SIN_PERMISO" -> "Tu rol no permite esta operación.";
            case "VERSION_DESACTUALIZADA", "VERSION_O_CAMBIO_EN_CONFLICTO" ->
                    "Otro usuario cambió el desastre. Recarga la página y vuelve a intentarlo.";
            case "CREDENCIALES_INVALIDAS" -> "Usuario o contraseña incorrectos.";
            case "USUARIO_EXISTENTE" -> "Ese usuario ya existe.";
            case "DATOS_INVALIDOS" -> "Revisa los datos: usuario de 3 a 80 caracteres y contraseña de al menos 12.";
            case "DESASTRE_NO_ENCONTRADO" -> "El desastre no existe en tu organismo.";
            default -> codigo;
        };
    }

    private JsonNode leer(byte[] bytes) {
        if (bytes == null || bytes.length == 0) return null;
        try {
            return json.readTree(bytes);
        } catch (IOException e) {
            return null;
        }
    }
}
