package co.edu.javeriana.emergencias.infraestructura.mensajeria;

import co.edu.javeriana.emergencias.aplicacion.puerto.RegistroRepositorio;
import co.edu.javeriana.emergencias.dominio.desastres.Sistema;
import co.edu.javeriana.emergencias.infraestructura.correo.CorreoAvisos;
import co.edu.javeriana.emergencias.infraestructura.persistencia.BandejaEntrada;
import co.edu.javeriana.emergencias.infraestructura.persistencia.Json;
import jakarta.ejb.*;
import jakarta.inject.Inject;
import java.util.*;
import java.util.logging.Logger;

/**
 * Consumidor de los avisos de Reconstrucción (contrato C5, cola integracion.reconstruccion).
 *
 * Por cada aviso: si su idEvento ya se atendió, lo descarta (deduplicación); si no, lo anota como
 * RECIBIDO, envía el correo (solo PROYECTO_APROBADO, PROYECTO_CON_SOBRECOSTO y DESASTRE_CERRADO) y lo
 * marca ENVIADO antes de confirmarlo a RabbitMQ. Si el correo falla, RabbitMensajes lo reintenta a
 * los 5 s (cola .reintento) hasta 5 veces y luego lo deja en la cola .errores; un aviso mal formado va
 * directo a .errores. Entrega al menos una vez: si el proceso cae entre enviar y marcar, el correo
 * puede repetirse una vez (riesgo aceptado, ADR-27).
 */
@Singleton
@Startup
@TransactionAttribute(TransactionAttributeType.NOT_SUPPORTED)
public class ConsumidorReconstruccion {
    static final Set<String> CON_CORREO = Set.of("PROYECTO_APROBADO", "PROYECTO_CON_SOBRECOSTO", "DESASTRE_CERRADO");
    private static final int POR_CICLO = 20;
    private static final Logger LOG = Logger.getLogger(ConsumidorReconstruccion.class.getName());

    @EJB RabbitMensajes broker;
    @EJB BandejaEntrada bandeja;
    @EJB RegistroRepositorio repositorio;
    @Inject CorreoAvisos correo;

    @Schedule(hour = "*", minute = "*", second = "*/2", persistent = false)
    public void procesar() {
        for (int i = 0; i < POR_CICLO; i++)
            if (!broker.consumir(RabbitMensajes.RECONSTRUCCION, this::atender)) return;
    }

    void atender(String texto) {
        EventoReconstruccion e = Json.leer(texto, EventoReconstruccion.class);
        if (e == null) throw new IllegalArgumentException("Aviso vacio");
        e.validar();
        if (bandeja.yaAtendido(e.idEvento)) {
            LOG.fine("Aviso repetido descartado: " + e.idEvento);
            return;
        }
        int intento = bandeja.recibir(e.idEvento, e.tipo);
        if (!CON_CORREO.contains(e.tipo)) {
            bandeja.marcar(e.idEvento, "SIN_CORREO", "El tipo no lleva correo");
            return;
        }
        try {
            List<String> para = destinatarios(e.organismoId);
            for (String direccion : para) correo.enviar(direccion, asunto(e), texto(e), e.idEvento);
            bandeja.marcar(e.idEvento, "ENVIADO", "Correo a " + String.join(", ", para));
        } catch (RuntimeException error) {
            bandeja.marcar(e.idEvento, intento >= BandejaEntrada.MAX_INTENTOS ? "FALLIDO" : "PENDIENTE",
                    error.getClass().getSimpleName());
            throw error;
        }
    }

    /** Los sistemas suscritos del organismo; si no hay, la dirección de coordinación configurada. */
    private List<String> destinatarios(String organismoId) {
        List<String> para = new ArrayList<>();
        for (Sistema s : repositorio.sistemas())
            if (Objects.equals(s.organizacionId, organismoId) && s.correo != null) para.add(s.correo);
        if (para.isEmpty()) para.add(System.getenv().getOrDefault("NOTIFICACIONES_CORREO", "coordinacion@emergencias.local"));
        return para;
    }

    static String asunto(EventoReconstruccion e) {
        return switch (e.tipo) {
            case "PROYECTO_APROBADO" -> "Proyecto aprobado: " + e.dato("nombre");
            case "PROYECTO_CON_SOBRECOSTO" -> "Sobrecosto en el proyecto: " + e.dato("nombre");
            case "DESASTRE_CERRADO" -> "Desastre cerrado: reconstrucción terminada (" + e.desastreId + ")";
            default -> "Aviso de Reconstrucción: " + e.tipo;
        };
    }

    static String texto(EventoReconstruccion e) {
        StringBuilder t = new StringBuilder("Aviso del módulo de Reconstrucción\n\n");
        t.append("Tipo: ").append(e.tipo).append('\n');
        t.append("Ocurrió: ").append(e.ocurridoEn).append('\n');
        t.append("Desastre: ").append(e.desastreId).append('\n');
        if (e.proyectoId != null) t.append("Proyecto: ").append(e.proyectoId).append('\n');
        if (e.datos != null)
            new TreeMap<>(e.datos).forEach((k, v) -> {
                if (!k.equals("organizacion") && !k.equals("desastreId") && !k.equals("proyectoId"))
                    t.append(k).append(": ").append(v).append('\n');
            });
        t.append("\nIdentificador del aviso: ").append(e.idEvento).append('\n');
        return t.toString();
    }
}
