package com.pgdr.portal.sesion;

import jakarta.servlet.http.HttpSession;
import java.io.Serializable;
import java.time.Instant;

/**
 * Sesión del usuario en el portal. El token del núcleo vive solo aquí, en la sesión del servidor:
 * nunca llega al navegador (la cookie de sesión es HttpOnly). Cada llamada a un servicio lo
 * reenvía, y cada servicio lo valida contra el núcleo (contrato C4).
 */
public record Sesion(String token, String usuario, String rol, String organizacionId, Instant expiraEn)
        implements Serializable {

    public static final String ATRIBUTO = "pgdr.sesion";

    public static Sesion de(HttpSession http) {
        if (http == null) return null;
        Object valor = http.getAttribute(ATRIBUTO);
        if (!(valor instanceof Sesion s)) return null;
        if (s.expiraEn() != null && s.expiraEn().isBefore(Instant.now())) {
            http.removeAttribute(ATRIBUTO);
            return null;
        }
        return s;
    }

    public boolean esCoordinador() {
        return "COORDINADOR".equals(rol);
    }

    /** COORDINADOR y EVALUADOR registran información de campo (evaluaciones y caracterización). */
    public boolean puedeEvaluar() {
        return "COORDINADOR".equals(rol) || "EVALUADOR".equals(rol);
    }

    public boolean esEvaluador() {
        return "EVALUADOR".equals(rol);
    }

    /** Nombre del rol tal como lo ve Reconstrucción (COORDINADOR = autoridad, EVALUADOR = responsable). */
    public String rolReconstruccion() {
        return switch (rol) {
            case "COORDINADOR" -> "Autoridad";
            case "EVALUADOR" -> "Responsable";
            default -> "Auditor";
        };
    }
}
