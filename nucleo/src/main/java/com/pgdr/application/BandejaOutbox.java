package com.pgdr.application;

import jakarta.ejb.EJB;
import jakarta.ejb.Stateless;
import jakarta.ejb.TransactionAttribute;
import jakarta.ejb.TransactionAttributeType;
import jakarta.json.Json;
import jakarta.json.JsonObject;
import jakarta.persistence.EntityManager;
import jakarta.persistence.PersistenceContext;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

/**
 * Acceso transaccional a la bandeja de salida. Cada método corre en su propia transacción corta,
 * para que el envío HTTP a notificaciones ocurra fuera de cualquier transacción de la base.
 */
@Stateless
public class BandejaOutbox {
    /** Aviso listo para enviar: id de la fila y cuerpo JSON para notificaciones. */
    public record Pendiente(UUID id, String cuerpo) {}

    @PersistenceContext(unitName = "PGDR_PU")
    private EntityManager em;
    @EJB
    private RegistroIntegrado registro;

    /**
     * Reclama hasta {@code maximo} avisos pendientes por 30 segundos. FOR UPDATE SKIP LOCKED evita
     * que dos instancias tomen el mismo aviso; el reclamo con vencimiento permite reintentar si
     * el envío falla o la instancia se cae antes de marcarlo.
     */
    @TransactionAttribute(TransactionAttributeType.REQUIRES_NEW)
    public List<Pendiente> reclamar(int maximo) {
        List<Pendiente> pendientes = new ArrayList<>();
        for (Object raw : em.createNativeQuery("SELECT id,agregado_id,organizacion_id,payload_json,tipo FROM outbox_eventos "
                        + "WHERE publicado_en IS NULL AND tipo IN ('DESASTRE_REGISTRADO','DESASTRE_GRAVEDAD_CAMBIADA') "
                        + "AND (reclamado_hasta IS NULL OR reclamado_hasta < now()) ORDER BY creado_en LIMIT ?1 FOR UPDATE SKIP LOCKED")
                .setParameter(1, maximo).getResultList()) {
            Object[] fila = (Object[]) raw;
            UUID id = UUID.fromString(fila[0].toString());
            em.createNativeQuery("UPDATE outbox_eventos SET reclamado_hasta=now()+interval '30 seconds' WHERE id=?1")
                    .setParameter(1, id).executeUpdate();
            pendientes.add(new Pendiente(id, cuerpo(fila).toString()));
        }
        return pendientes;
    }

    @TransactionAttribute(TransactionAttributeType.REQUIRES_NEW)
    public void marcarPublicado(UUID id) {
        em.createNativeQuery("UPDATE outbox_eventos SET publicado_en=now(), reclamado_hasta=NULL WHERE id=?1")
                .setParameter(1, id).executeUpdate();
    }

    /** El cuerpo que espera notificaciones: la foto del desastre, con su referencia y su organismo. */
    private JsonObject cuerpo(Object[] fila) {
        JsonObject snapshot = registro.leer(fila[3].toString());
        if (!snapshot.containsKey("tipo")) {
            var originales = em.createNativeQuery("SELECT anterior FROM desastre_cambios WHERE desastre_id=?1 ORDER BY version LIMIT 1")
                    .setParameter(1, fila[1]).getResultList();
            snapshot = originales.isEmpty() ? registro.datos(UUID.fromString(fila[1].toString()))
                    : registro.leer(originales.get(0).toString());
        }
        String ref = snapshot.getString("id");
        return Json.createObjectBuilder(snapshot).add("eventoId", ref)
                .add("id", fila[4].toString().equals("DESASTRE_REGISTRADO") ? ref : fila[0].toString())
                .add("organizacionId", fila[2].toString()).build();
    }

    /** Purga las sesiones vencidas (sus tokens ya no sirven). */
    @TransactionAttribute(TransactionAttributeType.REQUIRES_NEW)
    public int purgarSesionesVencidas() {
        return em.createNativeQuery("DELETE FROM sesiones WHERE expira_en < now()").executeUpdate();
    }
}
