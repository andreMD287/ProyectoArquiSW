package com.pgdr.portal.web;

import com.pgdr.portal.cliente.ErrorServicio;
import com.pgdr.portal.cliente.NucleoCliente;
import com.pgdr.portal.cliente.ReconstruccionCliente;
import com.pgdr.portal.sesion.Sesion;
import com.pgdr.reconstruccion.grpc.ProyectoMsg;
import com.pgdr.reconstruccion.grpc.SeguimientoResponse;
import java.util.List;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestAttribute;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.servlet.mvc.support.RedirectAttributes;

/**
 * Reconstrucción física (mínimo b): seguimiento por desastre, creación de proyectos desde las
 * evaluaciones de daño y ciclo de vida del proyecto. Todo por gRPC hacia Reconstrucción; las
 * reglas (RN-xx) las aplica Reconstrucción y el portal muestra su mensaje.
 */
@Controller
public class ReconstruccionController {
    private final ReconstruccionCliente reconstruccion;
    private final NucleoCliente nucleo;

    public ReconstruccionController(ReconstruccionCliente reconstruccion, NucleoCliente nucleo) {
        this.reconstruccion = reconstruccion;
        this.nucleo = nucleo;
    }

    @GetMapping("/reconstruccion/{ref}")
    public String seguimiento(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable String ref, Model modelo) {
        modelo.addAttribute("ref", ref);
        modelo.addAttribute("d", nucleo.desastre(sesion.token(), ref));
        modelo.addAttribute("s", reconstruccion.seguimiento(sesion.token(), ref));
        if (sesion.esCoordinador()) {
            try {
                modelo.addAttribute("evaluaciones", reconstruccion.evaluaciones(sesion.token(), ref).getEvaluacionesList());
            } catch (ErrorServicio e) {
                modelo.addAttribute("avisoEvaluaciones", e.paraUsuario());
            }
        }
        return "reconstruccion/seguimiento";
    }

    @PostMapping("/reconstruccion/{ref}/iniciar")
    public String iniciar(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable String ref, RedirectAttributes flash) {
        return Acciones.ejecutar(flash, "/reconstruccion/" + ref, "El desastre pasó a Reconstrucción.",
                () -> reconstruccion.iniciarReconstruccion(sesion.token(), ref));
    }

    @PostMapping("/reconstruccion/{ref}/cerrar")
    public String cerrar(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable String ref, RedirectAttributes flash) {
        return Acciones.ejecutar(flash, "/reconstruccion/" + ref, "El desastre quedó en Cierre.",
                () -> reconstruccion.cerrar(sesion.token(), ref));
    }

    @PostMapping("/reconstruccion/{ref}/proyectos")
    public String crear(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable String ref,
                        @RequestParam String nombre, @RequestParam(required = false) String tipo,
                        @RequestParam(name = "evaluaciones", required = false) List<String> evaluaciones,
                        RedirectAttributes flash) {
        return Acciones.ejecutar(flash, "/reconstruccion/" + ref, "Proyecto creado en estado Propuesto.",
                () -> reconstruccion.crear(sesion.token(), ref, nombre.trim(), tipo, evaluaciones == null ? List.of() : evaluaciones));
    }

    @GetMapping("/reconstruccion/{ref}/proyectos/{id}")
    public String proyecto(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable String ref,
                           @PathVariable String id, Model modelo) {
        SeguimientoResponse s = reconstruccion.seguimiento(sesion.token(), ref);
        ProyectoMsg p = s.getProyectosList().stream().filter(x -> x.getId().equals(id)).findFirst()
                .orElseThrow(() -> new ErrorServicio(404, "El proyecto no existe en este desastre.", null));
        modelo.addAttribute("ref", ref);
        modelo.addAttribute("desastre", s.getDesastre());
        modelo.addAttribute("p", p);
        if (!sesion.esEvaluador())
            modelo.addAttribute("auditoria", reconstruccion.auditoria(sesion.token(), id).getRegistrosList());
        return "reconstruccion/proyecto";
    }

    @PostMapping("/reconstruccion/{ref}/proyectos/{id}/{accion}")
    public String accion(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable String ref, @PathVariable String id,
                         @PathVariable String accion, @RequestParam(required = false) String presupuesto,
                         @RequestParam(required = false) String fuente, @RequestParam(required = false) String responsable,
                         @RequestParam(required = false) String contratista, @RequestParam(required = false) String inicio,
                         @RequestParam(required = false) String fin, @RequestParam(required = false) Integer porcentaje,
                         @RequestParam(required = false) String valor, @RequestParam(required = false) String descripcion,
                         @RequestParam(required = false) String motivo, @RequestParam(required = false) String estado,
                         @RequestParam(required = false) Long version,
                         RedirectAttributes flash) {
        String t = sesion.token();
        String destino = "/reconstruccion/" + ref + "/proyectos/" + id;
        return switch (accion) {
            case "aprobar" -> Acciones.ejecutar(flash, destino, "Proyecto aprobado.",
                    () -> reconstruccion.aprobar(t, id, presupuesto, fuente, responsable, version));
            case "iniciar" -> Acciones.ejecutar(flash, destino, "Ejecución iniciada.",
                    () -> reconstruccion.iniciarEjecucion(t, id, contratista, inicio, fin, version));
            case "avance" -> Acciones.ejecutar(flash, destino, "Avance registrado.",
                    () -> reconstruccion.avance(t, id, porcentaje == null ? -1 : porcentaje, descripcion, motivo, version));
            case "costo" -> Acciones.ejecutar(flash, destino, "Costo registrado.",
                    () -> reconstruccion.costo(t, id, valor, descripcion, version));
            case "evidencia" -> Acciones.ejecutar(flash, destino, "Evidencia adjunta.",
                    () -> reconstruccion.evidencia(t, id, descripcion, version));
            case "presupuesto" -> Acciones.ejecutar(flash, destino, "Presupuesto modificado.",
                    () -> reconstruccion.presupuesto(t, id, presupuesto, motivo, version));
            case "estado" -> Acciones.ejecutar(flash, destino, "Estado cambiado a " + estado + ".",
                    () -> reconstruccion.estado(t, id, estado, motivo, version));
            default -> throw new ErrorServicio(404, "Acción desconocida.", null);
        };
    }
}
