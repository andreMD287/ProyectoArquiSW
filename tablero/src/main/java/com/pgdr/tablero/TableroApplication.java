package com.pgdr.tablero;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.web.servlet.ServletComponentScan;

/**
 * UD-10 Servicio de tablero. Compone el tablero de un desastre con tres servicios dueños de sus
 * datos (núcleo, servidor y Reconstrucción) mediante un servlet asíncrono y multihilo (ADR-24).
 * No tiene interfaz: lo consume el portal web .NET (UD-09), que reenvía el token del usuario.
 * ServletComponentScan registra el TableroServlet.
 */
@SpringBootApplication
@ServletComponentScan
public class TableroApplication {
    public static void main(String[] args) {
        SpringApplication.run(TableroApplication.class, args);
    }
}
