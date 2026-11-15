package com.pgdr.portal.tablero;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.function.Supplier;

/**
 * Arma el tablero de un desastre con tres fuentes independientes, cada una dueña de sus datos:
 * el desastre (núcleo), sus evaluaciones de daño (servidor) y los proyectos de reconstrucción
 * (Reconstrucción, por gRPC). En modo paralelo las tres consultas corren a la vez en el pool
 * acotado, cada una con su propio límite de tiempo: la latencia es la de la más lenta y no la
 * suma, y si una fuente falla o se demora el tablero responde con las demás (degradación
 * parcial). El modo secuencial existe solo para medir la diferencia (ADR-24).
 */
public class Tablero {

    /** Resultado de una fuente: sus datos o el error, cuánto tardó y en qué hilo corrió. */
    public record Fuente(String nombre, Object datos, String estado, long ms, String hilo, String error) {
        static Fuente vencida(String nombre, long limiteMs) {
            return new Fuente(nombre, null, "tiempo-agotado", limiteMs, null,
                    "No respondió en " + limiteMs + " ms; el tablero sigue con las demás fuentes.");
        }

        boolean disponible() {
            return "ok".equals(estado);
        }
    }

    /** Las tres consultas, ya ligadas al token del usuario y al desastre. */
    public record Consultas(Supplier<Object> desastre, Supplier<Object> evaluaciones, Supplier<Object> reconstruccion) {}

    private final ExecutorService pool;
    private final long limiteFuenteMs;

    public Tablero(ExecutorService pool, long limiteFuenteMs) {
        this.pool = pool;
        this.limiteFuenteMs = limiteFuenteMs;
    }

    /**
     * Lanza las consultas. Si el pool y su cola están llenos, supplyAsync lanza
     * RejectedExecutionException de inmediato y el servlet responde 503.
     */
    public CompletableFuture<Map<String, Object>> armar(String ref, Consultas c, boolean secuencial) {
        long inicio = System.nanoTime();
        if (secuencial) {
            return CompletableFuture.supplyAsync(() -> {
                List<Fuente> fuentes = List.of(medir("desastre", c.desastre()), medir("evaluaciones", c.evaluaciones()),
                        medir("reconstruccion", c.reconstruccion()));
                return componer(ref, "secuencial", fuentes, inicio);
            }, pool);
        }
        CompletableFuture<Fuente> d = fuente("desastre", c.desastre());
        CompletableFuture<Fuente> e = fuente("evaluaciones", c.evaluaciones());
        CompletableFuture<Fuente> r = fuente("reconstruccion", c.reconstruccion());
        return CompletableFuture.allOf(d, e, r)
                .thenApply(v -> componer(ref, "paralelo", List.of(d.join(), e.join(), r.join()), inicio));
    }

    private CompletableFuture<Fuente> fuente(String nombre, Supplier<Object> trabajo) {
        return CompletableFuture.supplyAsync(() -> medir(nombre, trabajo), pool)
                .completeOnTimeout(Fuente.vencida(nombre, limiteFuenteMs), limiteFuenteMs, TimeUnit.MILLISECONDS);
    }

    static Fuente medir(String nombre, Supplier<Object> trabajo) {
        long t = System.nanoTime();
        String hilo = Thread.currentThread().getName();
        try {
            Object datos = trabajo.get();
            return new Fuente(nombre, datos, "ok", ms(t), hilo, null);
        } catch (RuntimeException ex) {
            return new Fuente(nombre, null, "error", ms(t), hilo, ex.getMessage());
        }
    }

    static Map<String, Object> componer(String ref, String modo, List<Fuente> fuentes, long inicio) {
        Map<String, Object> cuerpo = new LinkedHashMap<>();
        cuerpo.put("desastreRef", ref);
        cuerpo.put("modo", modo);
        cuerpo.put("totalMs", ms(inicio));
        Map<String, Object> detalle = new LinkedHashMap<>();
        List<String> noDisponibles = new ArrayList<>();
        for (Fuente f : fuentes) {
            Map<String, Object> m = new LinkedHashMap<>();
            m.put("estado", f.estado());
            m.put("ms", f.ms());
            m.put("hilo", f.hilo());
            if (f.error() != null) m.put("error", f.error());
            detalle.put(f.nombre(), m);
            if (!f.disponible()) noDisponibles.add(f.nombre());
            cuerpo.put(f.nombre(), f.datos());
        }
        cuerpo.put("fuentes", detalle);
        cuerpo.put("fuentesNoDisponibles", noDisponibles);
        return cuerpo;
    }

    private static long ms(long desdeNanos) {
        return TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - desdeNanos);
    }
}
