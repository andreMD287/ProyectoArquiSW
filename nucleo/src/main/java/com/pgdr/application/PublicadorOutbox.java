package com.pgdr.application;

import jakarta.ejb.EJB;
import jakarta.ejb.Schedule;
import jakarta.ejb.Singleton;
import jakarta.ejb.Startup;
import jakarta.ejb.TransactionAttribute;
import jakarta.ejb.TransactionAttributeType;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.logging.Logger;

/**
 * Despacha la bandeja de salida hacia notificaciones (Taller 3). El cambio y su aviso se guardaron
 * en la misma transacción; aquí el aviso se reclama en una transacción corta, se envía por HTTP
 * fuera de toda transacción y se marca como publicado en otra. Un envío fallido se reintenta
 * cuando vence el reclamo (al menos una vez: notificaciones descarta repetidos por id).
 */
@Singleton
@Startup
public class PublicadorOutbox {
    private static final Logger LOG = Logger.getLogger(PublicadorOutbox.class.getName());
    private final HttpClient http = HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(2)).build();

    @EJB
    private BandejaOutbox bandeja;

    @Schedule(hour = "*", minute = "*", second = "*/1", persistent = false)
    @TransactionAttribute(TransactionAttributeType.NOT_SUPPORTED)
    public void publicar() {
        for (BandejaOutbox.Pendiente p : bandeja.reclamar(20)) {
            try {
                var req = HttpRequest.newBuilder(URI.create(System.getenv("NOTIFICACIONES_URL") + "/desastres"))
                        .timeout(Duration.ofSeconds(3)).header("Content-Type", "application/json")
                        .POST(HttpRequest.BodyPublishers.ofString(p.cuerpo())).build();
                int estado = http.send(req, HttpResponse.BodyHandlers.discarding()).statusCode();
                if (estado == 200 || estado == 202) bandeja.marcarPublicado(p.id());
                else LOG.warning("Notificación pendiente; HTTP " + estado);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                return;
            } catch (Exception e) {
                LOG.warning("Se reintentará la notificación: " + e.getClass().getSimpleName());
            }
        }
    }

    /** Cada 15 minutos borra las sesiones vencidas. */
    @Schedule(hour = "*", minute = "*/15", persistent = false)
    @TransactionAttribute(TransactionAttributeType.NOT_SUPPORTED)
    public void purgarSesiones() {
        int borradas = bandeja.purgarSesionesVencidas();
        if (borradas > 0) LOG.info("Sesiones vencidas purgadas: " + borradas);
    }
}
