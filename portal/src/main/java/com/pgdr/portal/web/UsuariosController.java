package com.pgdr.portal.web;

import com.pgdr.portal.cliente.ErrorServicio;
import com.pgdr.portal.cliente.NucleoCliente;
import com.pgdr.portal.sesion.Sesion;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestAttribute;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.servlet.mvc.support.RedirectAttributes;

/** Usuarios del organismo (solo COORDINADOR). El núcleo crea el usuario en el organismo de quien lo pide. */
@Controller
public class UsuariosController {
    private final NucleoCliente nucleo;

    public UsuariosController(NucleoCliente nucleo) {
        this.nucleo = nucleo;
    }

    @GetMapping("/usuarios")
    public String formulario(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion) {
        if (!sesion.esCoordinador()) throw new ErrorServicio(403, "Solo un coordinador administra los usuarios del organismo.", null);
        return "usuarios";
    }

    @PostMapping("/usuarios")
    public String crear(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @RequestParam String usuario,
                        @RequestParam String clave, @RequestParam String rol, RedirectAttributes flash) {
        return Acciones.ejecutar(flash, "/usuarios", "Usuario " + usuario.trim().toLowerCase() + " creado con rol " + rol + ".",
                () -> nucleo.crearUsuario(sesion.token(), usuario.trim(), clave, rol));
    }
}
