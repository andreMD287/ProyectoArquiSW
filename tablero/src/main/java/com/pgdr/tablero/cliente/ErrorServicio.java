package com.pgdr.tablero.cliente;

/**
 * Respuesta de error de un servicio (núcleo, servidor o Reconstrucción), con el estado HTTP
 * equivalente, el mensaje para el usuario y la regla de negocio si la hay (RN-xx).
 */
public class ErrorServicio extends RuntimeException {
    private final int estado;
    private final String regla;

    public ErrorServicio(int estado, String mensaje, String regla) {
        super(mensaje);
        this.estado = estado;
        this.regla = regla;
    }

    public int estado() {
        return estado;
    }

    public String regla() {
        return regla;
    }

    /** Mensaje para mostrar en la página, con la regla si la hay. */
    public String paraUsuario() {
        return (regla == null || regla.isBlank() ? "" : regla + ": ") + getMessage();
    }
}
