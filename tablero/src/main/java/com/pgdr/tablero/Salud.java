package com.pgdr.tablero;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

/** Prueba de vida para el arranque y la verificación. */
@RestController
public class Salud {
    @GetMapping("/salud")
    public String salud() {
        return "ok";
    }
}
