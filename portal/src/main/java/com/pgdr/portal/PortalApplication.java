package com.pgdr.portal;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.web.servlet.ServletComponentScan;

/**
 * UD-09 Portal web. Presentación MVC de la plataforma: el DispatcherServlet de Spring es el
 * controlador frontal, las clases @Controller son los controladores, los modelos de vista vienen
 * de los servicios dueños de los datos y las vistas son plantillas Thymeleaf.
 * ServletComponentScan registra además el TableroServlet, un servlet asíncrono multihilo (ADR-24).
 */
@SpringBootApplication
@ServletComponentScan
public class PortalApplication {
    public static void main(String[] args) {
        SpringApplication.run(PortalApplication.class, args);
    }
}
