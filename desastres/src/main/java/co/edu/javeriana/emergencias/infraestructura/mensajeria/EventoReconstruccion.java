package co.edu.javeriana.emergencias.infraestructura.mensajeria;

import java.util.Map;

/** Sobre de un aviso de Reconstrucción (contrato C5). idEvento es único y no cambia en los reenvíos. */
public class EventoReconstruccion {
    public String idEvento;
    public String tipo;
    public String ocurridoEn;
    public String desastreId;
    public String proyectoId;
    public String organismoId;
    public Map<String, Object> datos;

    public void validar() {
        if (idEvento == null || idEvento.isBlank() || idEvento.length() > 64)
            throw new IllegalArgumentException("Aviso C5 sin idEvento valido");
        if (tipo == null || !tipo.matches("[A-Z_]{3,60}"))
            throw new IllegalArgumentException("Aviso C5 sin tipo valido");
    }

    public String dato(String clave) {
        Object valor = datos == null ? null : datos.get(clave);
        return valor == null ? "" : valor.toString();
    }
}
