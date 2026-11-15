package com.pgdr.portal.web;

import com.pgdr.portal.cliente.ClienteHttp;
import com.pgdr.portal.cliente.NucleoCliente;
import com.pgdr.portal.cliente.ServidorCliente;
import com.pgdr.portal.sesion.Sesion;
import java.util.UUID;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestAttribute;
import org.springframework.web.bind.annotation.RequestParam;

/** Evaluaciones de daño (mínimo d): lista por desastre, detalle con historial de versiones y foto. */
@Controller
public class EvaluacionesController {
    private final ServidorCliente servidor;
    private final NucleoCliente nucleo;

    public EvaluacionesController(ServidorCliente servidor, NucleoCliente nucleo) {
        this.servidor = servidor;
        this.nucleo = nucleo;
    }

    @GetMapping("/evaluaciones")
    public String lista(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @RequestParam(required = false) String desastre,
                        Model modelo) {
        modelo.addAttribute("evaluaciones", servidor.evaluaciones(sesion.token(), desastre));
        modelo.addAttribute("desastres", nucleo.desastres(sesion.token()));
        modelo.addAttribute("desastre", desastre == null ? "" : desastre);
        return "evaluaciones/lista";
    }

    @GetMapping("/evaluaciones/{id}")
    public String detalle(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable UUID id, Model modelo) {
        modelo.addAttribute("e", servidor.evaluacion(sesion.token(), id));
        modelo.addAttribute("historial", servidor.historial(sesion.token(), id));
        return "evaluaciones/detalle";
    }

    /** La foto pasa por el portal con el token del usuario; el navegador nunca ve el token. */
    @GetMapping("/evaluaciones/{id}/foto")
    public ResponseEntity<byte[]> foto(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable UUID id) {
        ClienteHttp.Respuesta r = servidor.foto(sesion.token(), id);
        if (r.estado() != 200) return ResponseEntity.notFound().build();
        MediaType tipo = r.tipo() == null ? MediaType.APPLICATION_OCTET_STREAM : MediaType.parseMediaType(r.tipo());
        return ResponseEntity.ok().contentType(tipo).header("Cache-Control", "private, max-age=300").body(r.bytes());
    }
}
