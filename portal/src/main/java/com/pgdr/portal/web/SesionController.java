package com.pgdr.portal.web;

import com.fasterxml.jackson.databind.JsonNode;
import com.pgdr.portal.cliente.ErrorServicio;
import com.pgdr.portal.cliente.NucleoCliente;
import com.pgdr.portal.sesion.Sesion;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpSession;
import java.time.Instant;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseBody;

/** Inicio y cierre de sesión. La identidad es la del núcleo: el portal no guarda contraseñas. */
@Controller
public class SesionController {
    private final NucleoCliente nucleo;

    public SesionController(NucleoCliente nucleo) {
        this.nucleo = nucleo;
    }

    @GetMapping("/")
    public String inicio() {
        return "redirect:/desastres";
    }

    @GetMapping("/salud")
    @ResponseBody
    public String salud() {
        return "ok";
    }

    @GetMapping("/login")
    public String formulario(@RequestParam(required = false) String siguiente, Model modelo) {
        modelo.addAttribute("siguiente", siguiente == null ? "" : siguiente);
        return "login";
    }

    @PostMapping("/login")
    public String entrar(@RequestParam String usuario, @RequestParam String clave,
                         @RequestParam(required = false) String siguiente, HttpServletRequest request, Model modelo) {
        try {
            JsonNode r = nucleo.login(usuario.trim(), clave);
            HttpSession anterior = request.getSession(false);
            if (anterior != null) anterior.invalidate();   // sesión nueva al entrar (evita fijación de sesión)
            HttpSession http = request.getSession(true);
            http.setAttribute(Sesion.ATRIBUTO, new Sesion(r.path("accessToken").asText(), r.path("usuario").asText(),
                    r.path("rol").asText(), r.path("organizacionId").asText(), Instant.parse(r.path("expiraEn").asText())));
            boolean destinoPropio = siguiente != null && siguiente.startsWith("/") && !siguiente.startsWith("//");
            return "redirect:" + (destinoPropio ? siguiente : "/desastres");
        } catch (ErrorServicio e) {
            modelo.addAttribute("error", e.estado() == 401 ? "Usuario o contraseña incorrectos." : e.paraUsuario());
            modelo.addAttribute("usuario", usuario);
            modelo.addAttribute("siguiente", siguiente == null ? "" : siguiente);
            return "login";
        }
    }

    @PostMapping("/logout")
    public String salir(HttpServletRequest request) {
        HttpSession http = request.getSession(false);
        Sesion sesion = Sesion.de(http);
        if (sesion != null) {
            try {
                nucleo.logout(sesion.token());   // revoca el token también en el núcleo
            } catch (ErrorServicio ignorado) {
                // Si el núcleo no responde, la sesión del portal se cierra igual.
            }
        }
        if (http != null) http.invalidate();
        return "redirect:/login?salida";
    }
}
