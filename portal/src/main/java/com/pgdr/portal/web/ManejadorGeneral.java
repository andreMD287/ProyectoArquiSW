package com.pgdr.portal.web;

import com.pgdr.portal.cliente.ErrorServicio;
import com.pgdr.portal.sesion.Sesion;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.servlet.http.HttpSession;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.ControllerAdvice;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.ModelAttribute;

/** Lo común a todas las vistas: la sesión en el modelo y la página de error de un servicio. */
@ControllerAdvice
public class ManejadorGeneral {

    @ModelAttribute("sesion")
    public Sesion sesion(HttpServletRequest request) {
        return (Sesion) request.getAttribute(Sesion.ATRIBUTO);
    }

    @ExceptionHandler(ErrorServicio.class)
    public String error(ErrorServicio e, HttpServletRequest request, HttpServletResponse response, Model modelo) {
        if (e.estado() == 401) {   // el token venció en el núcleo: se cierra la sesión del portal
            HttpSession http = request.getSession(false);
            if (http != null) http.invalidate();
            return "redirect:/login?vencida";
        }
        response.setStatus(e.estado() >= 400 && e.estado() < 600 ? e.estado() : 500);
        modelo.addAttribute("estado", e.estado());
        modelo.addAttribute("mensaje", e.paraUsuario());
        return "error";
    }
}
