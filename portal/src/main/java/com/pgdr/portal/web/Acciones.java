package com.pgdr.portal.web;

import com.pgdr.portal.cliente.ErrorServicio;
import org.springframework.web.servlet.mvc.support.RedirectAttributes;

/** Ejecuta una acción de un formulario y deja el resultado como mensaje para la página siguiente. */
final class Acciones {
    private Acciones() {}

    static String ejecutar(RedirectAttributes flash, String destino, String exito, Runnable accion) {
        try {
            accion.run();
            flash.addFlashAttribute("ok", exito);
        } catch (ErrorServicio e) {
            if (e.estado() == 401) throw e;   // lo atiende ManejadorGeneral: vuelve al inicio de sesión
            flash.addFlashAttribute("error", e.paraUsuario());
        } catch (IllegalArgumentException e) {
            flash.addFlashAttribute("error", "Revisa los datos del formulario: " + e.getMessage());
        }
        return "redirect:" + destino;
    }
}
