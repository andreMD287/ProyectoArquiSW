package com.pgdr.portal.web;

import com.pgdr.portal.cliente.NucleoCliente;
import com.pgdr.portal.sesion.Sesion;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestAttribute;

/**
 * Mapa general (mínimo c): todos los desastres del organismo con su área afectada y su fase.
 * El mapa de un desastre, con evaluaciones y proyectos, está en su tablero (/desastres/{ref}).
 */
@Controller
public class MapaController {
    private final NucleoCliente nucleo;

    public MapaController(NucleoCliente nucleo) {
        this.nucleo = nucleo;
    }

    @GetMapping("/mapa")
    public String mapa(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, Model modelo) {
        modelo.addAttribute("desastres", nucleo.desastres(sesion.token()));
        return "mapa";
    }
}
