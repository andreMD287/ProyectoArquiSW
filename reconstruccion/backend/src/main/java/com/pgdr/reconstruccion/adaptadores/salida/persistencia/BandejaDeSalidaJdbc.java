package com.pgdr.reconstruccion.adaptadores.salida.persistencia;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.pgdr.reconstruccion.aplicacion.puertos.PublicadorEventos;
import java.sql.Timestamp;
import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

/**
 * Implementa PublicadorEventos como bandeja de salida: el evento se guarda en la misma transacción
 * del cambio y queda pendiente (publicado_en nulo) hasta que un despachador lo envíe a la mensajería.
 * Así un evento nunca se pierde ni se publica sin que el cambio exista.
 */
@Repository
public class BandejaDeSalidaJdbc implements PublicadorEventos {
    private final JdbcTemplate jdbc;
    private final ObjectMapper json;

    public BandejaDeSalidaJdbc(JdbcTemplate jdbc, ObjectMapper json) {
        this.jdbc = jdbc;
        this.json = json;
    }

    @Override
    public void publicar(String tipo, UUID agregadoId, Map<String, String> datos) {
        try {
            String id = UUID.randomUUID().toString();
            Instant ahora = Instant.now();
            // Sobre del contrato C5: el consumidor descarta repetidos por idEvento.
            Map<String, Object> sobre = new LinkedHashMap<>();
            sobre.put("idEvento", id);
            sobre.put("tipo", tipo);
            sobre.put("ocurridoEn", ahora.toString());
            sobre.put("desastreId", datos.get("desastreId"));
            sobre.put("proyectoId", datos.get("proyectoId"));
            sobre.put("organismoId", datos.get("organizacion"));
            sobre.put("datos", datos);
            jdbc.update("INSERT INTO outbox_eventos (id, tipo, agregado_id, payload_json, creado_en, publicado_en) "
                            + "VALUES (?,?,?,?,?,NULL)",
                    id, tipo, agregadoId.toString(), json.writeValueAsString(sobre), Timestamp.from(ahora));
        } catch (JsonProcessingException e) {
            throw new IllegalStateException("No se pudo serializar el evento " + tipo, e);
        }
    }
}
