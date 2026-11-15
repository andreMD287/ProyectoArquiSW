package com.pgdr.portal.tablero;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.pgdr.portal.sesion.Sesion;
import jakarta.servlet.AsyncContext;
import jakarta.servlet.AsyncEvent;
import jakarta.servlet.AsyncListener;
import jakarta.servlet.annotation.WebServlet;
import jakarta.servlet.http.HttpServlet;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.Map;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.atomic.AtomicBoolean;
import org.springframework.beans.factory.annotation.Value;

/**
 * Servlet asíncrono y multihilo del tablero de un desastre (ADR-24). GET /tablero?desastre=REF
 *
 * 1. request.startAsync() libera el hilo de Tomcat mientras se espera a los servicios.
 * 2. El pool acotado del tablero consulta en paralelo el núcleo, servidor y Reconstrucción.
 * 3. Cada fuente tiene su límite de tiempo; si una falla, se responde con las demás.
 * 4. Si el pool y su cola están llenos, responde 503 con Retry-After en vez de encolar sin límite.
 *
 * Con ?modo=secuencial hace las tres consultas una tras otra, solo para medir la diferencia.
 */
@WebServlet(name = "servletTablero", urlPatterns = "/tablero", asyncSupported = true)
public class TableroServlet extends HttpServlet {

    private final Tablero tablero;
    private final FuentesTablero fuentes;
    private final ObjectMapper json;
    private final long limiteTotalMs;

    public TableroServlet(Tablero tablero, FuentesTablero fuentes, ObjectMapper json,
                          @Value("${portal.tablero.limite-total-ms}") long limiteTotalMs) {
        this.tablero = tablero;
        this.fuentes = fuentes;
        this.json = json;
        this.limiteTotalMs = limiteTotalMs;
    }

    @Override
    protected void doGet(HttpServletRequest req, HttpServletResponse resp) throws IOException {
        Sesion sesion = Sesion.de(req.getSession(false));
        if (sesion == null) {
            escribir(resp, 401, Map.of("error", "Inicia sesión para ver el tablero."));
            return;
        }
        String ref = req.getParameter("desastre");
        if (ref == null || !ref.matches("[A-Za-z0-9_-]{1,100}")) {
            escribir(resp, 400, Map.of("error", "Indica el desastre: /tablero?desastre=REFERENCIA"));
            return;
        }
        boolean secuencial = "secuencial".equals(req.getParameter("modo"));

        AsyncContext ctx = req.startAsync();
        ctx.setTimeout(limiteTotalMs);
        AtomicBoolean respondido = new AtomicBoolean(false);
        ctx.addListener(new AsyncListener() {
            @Override
            public void onTimeout(AsyncEvent e) throws IOException {
                if (respondido.compareAndSet(false, true)) {
                    escribir((HttpServletResponse) ctx.getResponse(), 504,
                            Map.of("error", "El tablero no se completó en " + limiteTotalMs + " ms."));
                    ctx.complete();
                }
            }

            @Override public void onComplete(AsyncEvent e) {}
            @Override public void onError(AsyncEvent e) {}
            @Override public void onStartAsync(AsyncEvent e) {}
        });

        try {
            tablero.armar(ref, fuentes.para(sesion.token(), ref), secuencial).whenComplete((cuerpo, error) -> {
                if (!respondido.compareAndSet(false, true)) return;
                try {
                    HttpServletResponse r = (HttpServletResponse) ctx.getResponse();
                    if (error != null) escribir(r, 500, Map.of("error", "No se pudo armar el tablero."));
                    else escribir(r, 200, cuerpo);
                } catch (IOException ignorada) {
                    // El navegador ya se fue; no hay a quién responder.
                } finally {
                    ctx.complete();
                }
            });
        } catch (RejectedExecutionException saturado) {
            if (respondido.compareAndSet(false, true)) {
                resp.setHeader("Retry-After", "2");
                escribir(resp, 503, Map.of("error", "El tablero está atendiendo demasiadas consultas; reintenta en 2 s."));
                ctx.complete();
            }
        }
    }

    private void escribir(HttpServletResponse resp, int estado, Object cuerpo) throws IOException {
        resp.setStatus(estado);
        resp.setContentType("application/json");
        resp.setCharacterEncoding("UTF-8");
        resp.setHeader("Cache-Control", "no-store");
        json.writeValue(resp.getOutputStream(), cuerpo);
    }
}
