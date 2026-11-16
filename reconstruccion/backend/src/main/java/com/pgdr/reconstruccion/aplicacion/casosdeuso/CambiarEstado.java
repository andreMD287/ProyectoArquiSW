package com.pgdr.reconstruccion.aplicacion.casosdeuso;

import com.pgdr.reconstruccion.dominio.Actor;
import com.pgdr.reconstruccion.dominio.EstadoProyecto;
import com.pgdr.reconstruccion.dominio.Proyecto;
import com.pgdr.reconstruccion.dominio.Rol;
import java.util.UUID;

/** CU-07: suspender, reanudar, finalizar o cancelar (RN-15 a RN-17, RN-21). Cancelar es solo de la autoridad. */
public class CambiarEstado extends CasoDeUso {
    public CambiarEstado(Dependencias d) {
        super(d);
    }

    public Proyecto ejecutar(Actor actor, UUID proyectoId, EstadoProyecto destino, String motivo) {
        return ejecutar(actor, proyectoId, destino, motivo, null);
    }

    /** Con versionEsperada, rechaza el cambio si otro usuario modificó el proyecto desde que lo vio. */
    public Proyecto ejecutar(Actor actor, UUID proyectoId, EstadoProyecto destino, String motivo,
                             Long versionEsperada) {
        autorizar(actor, Rol.AUTORIDAD, Rol.RESPONSABLE);
        if (destino == EstadoProyecto.CANCELADO) autorizar(actor, Rol.AUTORIDAD);
        return ejecutarAuditado(actor, "CAMBIAR_ESTADO", PROYECTO, proyectoId, null, motivo, () -> {
            Proyecto p = cargarProyecto(actor, proyectoId, versionEsperada);
            exigirResponsableAsignado(actor, p);
            EstadoProyecto anterior = p.cambiarEstado(destino, motivo);
            d.proyectos().actualizar(p);
            auditar(actor, "CAMBIAR_ESTADO", PROYECTO, p.id(), p.desastreId(), anterior.name(), p.estado().name(), motivo);
            d.eventos().publicar("PROYECTO_ESTADO_CAMBIADO", p.id(),
                    datosProyecto(p, "estadoAnterior", anterior.name(), "motivo", motivo == null ? "" : motivo));
            return p;
        });
    }
}
