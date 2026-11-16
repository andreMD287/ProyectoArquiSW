package com.pgdr.reconstruccion.aplicacion.casosdeuso;

import com.pgdr.reconstruccion.dominio.Actor;
import com.pgdr.reconstruccion.dominio.Proyecto;
import com.pgdr.reconstruccion.dominio.Rol;
import java.math.BigDecimal;
import java.util.UUID;

/** CU-06: modificar el presupuesto; el cambio y su auditoría se confirman juntos (RN-12, RN-17). */
public class ModificarPresupuesto extends CasoDeUso {
    public ModificarPresupuesto(Dependencias d) {
        super(d);
    }

    public Proyecto ejecutar(Actor actor, UUID proyectoId, BigDecimal nuevo, String motivo) {
        return ejecutar(actor, proyectoId, nuevo, motivo, null);
    }

    /** Con versionEsperada, rechaza el cambio si otro usuario modificó el proyecto desde que lo vio. */
    public Proyecto ejecutar(Actor actor, UUID proyectoId, BigDecimal nuevo, String motivo,
                             Long versionEsperada) {
        autorizar(actor, Rol.AUTORIDAD);
        return ejecutarAuditado(actor, "MODIFICAR_PRESUPUESTO", PROYECTO, proyectoId, null, motivo, () -> {
            Proyecto p = cargarProyecto(actor, proyectoId, versionEsperada);
            BigDecimal anterior = p.modificarPresupuesto(nuevo, motivo);
            d.proyectos().actualizar(p);
            auditar(actor, "MODIFICAR_PRESUPUESTO", PROYECTO, p.id(), p.desastreId(), anterior.toPlainString(),
                    nuevo.toPlainString(), motivo);
            d.eventos().publicar("PRESUPUESTO_MODIFICADO", p.id(), datosProyecto(p,
                    "anterior", anterior.toPlainString(), "nuevo", nuevo.toPlainString(), "motivo", motivo));
            return p;
        });
    }
}
