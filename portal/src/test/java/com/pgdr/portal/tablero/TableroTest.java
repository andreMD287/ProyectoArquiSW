package com.pgdr.portal.tablero;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;
import java.util.Map;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.Supplier;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;

/** El tablero multihilo: paralelo frente a secuencial, degradación parcial y pool acotado (ADR-24). */
class TableroTest {
    private ExecutorService pool = pool(8, 16);

    private static ExecutorService pool(int hilos, int cola) {
        AtomicInteger n = new AtomicInteger();
        return new ThreadPoolExecutor(hilos, hilos, 30, TimeUnit.SECONDS, new ArrayBlockingQueue<>(cola),
                r -> new Thread(r, "tablero-" + n.incrementAndGet()), new ThreadPoolExecutor.AbortPolicy());
    }

    @AfterEach
    void cerrar() {
        pool.shutdownNow();
    }

    private static Supplier<Object> tarda(long ms, Object resultado) {
        return () -> {
            try {
                Thread.sleep(ms);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }
            return resultado;
        };
    }

    @SuppressWarnings("unchecked")
    private static Map<String, Object> fuente(Map<String, Object> cuerpo, String nombre) {
        return (Map<String, Object>) ((Map<String, Object>) cuerpo.get("fuentes")).get(nombre);
    }

    @Test
    void enParaleloTardaLoDeLaFuenteMasLentaYNoLaSuma() throws Exception {
        Tablero t = new Tablero(pool, 2000);
        Tablero.Consultas c = new Tablero.Consultas(tarda(300, "d"), tarda(300, "e"), tarda(300, "r"));
        Map<String, Object> paralelo = t.armar("REF", c, false).get(5, TimeUnit.SECONDS);
        Map<String, Object> secuencial = t.armar("REF", c, true).get(5, TimeUnit.SECONDS);
        assertTrue((long) paralelo.get("totalMs") < 600, "paralelo: " + paralelo.get("totalMs"));
        assertTrue((long) secuencial.get("totalMs") >= 900, "secuencial: " + secuencial.get("totalMs"));
        assertEquals(List.of(), paralelo.get("fuentesNoDisponibles"));
        // Cada fuente corrió en su propio hilo del pool del tablero.
        String h1 = (String) fuente(paralelo, "desastre").get("hilo");
        String h2 = (String) fuente(paralelo, "evaluaciones").get("hilo");
        assertTrue(h1.startsWith("tablero-"));
        assertNotEquals(h1, h2);
    }

    @Test
    void unaFuenteLentaNoDetieneElTablero() throws Exception {
        Tablero t = new Tablero(pool, 400);
        Map<String, Object> r = t.armar("REF", new Tablero.Consultas(tarda(50, "d"), tarda(50, "e"), tarda(3000, "r")), false)
                .get(5, TimeUnit.SECONDS);
        assertTrue((long) r.get("totalMs") < 1000, "total: " + r.get("totalMs"));
        assertEquals("tiempo-agotado", fuente(r, "reconstruccion").get("estado"));
        assertEquals(List.of("reconstruccion"), r.get("fuentesNoDisponibles"));
        assertEquals("d", r.get("desastre"));
        assertNull(r.get("reconstruccion"));
    }

    @Test
    void unaFuenteConErrorDegradaSoloEsaParte() throws Exception {
        Tablero t = new Tablero(pool, 1000);
        Supplier<Object> falla = () -> {
            throw new IllegalStateException("servidor caído");
        };
        Map<String, Object> r = t.armar("REF", new Tablero.Consultas(tarda(10, "d"), falla, tarda(10, "r")), false)
                .get(5, TimeUnit.SECONDS);
        assertEquals("error", fuente(r, "evaluaciones").get("estado"));
        assertEquals("servidor caído", fuente(r, "evaluaciones").get("error"));
        assertEquals(List.of("evaluaciones"), r.get("fuentesNoDisponibles"));
        assertEquals("r", r.get("reconstruccion"));
    }

    @Test
    void conElPoolYLaColaLlenosRechazaEnVezDeEncolarSinLimite() {
        pool.shutdownNow();
        pool = pool(1, 1);
        Tablero t = new Tablero(pool, 5000);
        Tablero.Consultas lentas = new Tablero.Consultas(tarda(1000, "d"), tarda(1000, "e"), tarda(1000, "r"));
        assertThrows(RejectedExecutionException.class, () -> t.armar("REF", lentas, false));
    }
}
