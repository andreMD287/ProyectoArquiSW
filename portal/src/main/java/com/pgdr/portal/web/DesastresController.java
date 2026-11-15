package com.pgdr.portal.web;

import com.fasterxml.jackson.databind.JsonNode;
import com.pgdr.portal.cliente.NucleoCliente;
import com.pgdr.portal.sesion.Sesion;
import java.time.Instant;
import java.time.LocalDate;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestAttribute;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.servlet.mvc.support.RedirectAttributes;

/**
 * Desastres: lista, registro (fase Registro), tablero de un desastre, cambio de fase y
 * caracterización (contrato C2). El núcleo es el dueño; el portal solo presenta y reenvía.
 */
@Controller
public class DesastresController {
    static final List<String> FASES = List.of("REGISTRO", "CARACTERIZACION", "RECONSTRUCCION", "CIERRE");
    static final List<String> TIPOS = List.of("TERREMOTO", "INUNDACION", "DESLIZAMIENTO", "INCENDIO_FORESTAL",
            "HURACAN", "ERUPCION_VOLCANICA", "SEQUIA", "OTRO");
    private final NucleoCliente nucleo;

    public DesastresController(NucleoCliente nucleo) {
        this.nucleo = nucleo;
    }

    @GetMapping("/desastres")
    public String lista(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @RequestParam(required = false) String fase,
                        Model modelo) {
        JsonNode todos = nucleo.desastres(sesion.token());
        Map<String, Integer> porFase = new LinkedHashMap<>();
        FASES.forEach(f -> porFase.put(f, 0));
        List<JsonNode> filtrados = new ArrayList<>();
        for (JsonNode d : todos) {
            porFase.merge(d.path("fase").asText("REGISTRO"), 1, Integer::sum);
            if (fase == null || fase.isBlank() || fase.equals(d.path("fase").asText())) filtrados.add(d);
        }
        modelo.addAttribute("desastres", filtrados);
        modelo.addAttribute("porFase", porFase);
        modelo.addAttribute("total", todos.size());
        modelo.addAttribute("fase", fase == null ? "" : fase);
        return "desastres/lista";
    }

    @GetMapping("/desastres/nuevo")
    public String nuevo(Model modelo) {
        modelo.addAttribute("tipos", TIPOS);
        return "desastres/nuevo";
    }

    @PostMapping("/desastres")
    public String registrar(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @RequestParam String tipo,
                            @RequestParam String ubicacion, @RequestParam String descripcion,
                            @RequestParam String severidad, RedirectAttributes flash) {
        // Referencia legible y única; si el registro se repite, el núcleo lo trata como el mismo.
        String ref = "DES-" + LocalDate.now().format(DateTimeFormatter.BASIC_ISO_DATE) + "-"
                + UUID.randomUUID().toString().substring(0, 6).toUpperCase();
        return Acciones.ejecutar(flash, "/desastres/" + ref, "Desastre registrado en fase Registro.",
                () -> nucleo.registrar(sesion.token(), ref, tipo, ubicacion.trim(), descripcion.trim(), severidad,
                        Instant.now().toString()));
    }

    /** Tablero del desastre: los datos del núcleo aquí; el mapa y los resúmenes los trae /tablero. */
    @GetMapping("/desastres/{ref}")
    public String tablero(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable String ref, Model modelo) {
        JsonNode d = nucleo.desastre(sesion.token(), ref);
        modelo.addAttribute("d", d);
        modelo.addAttribute("ref", ref);
        modelo.addAttribute("historial", nucleo.historial(sesion.token(), ref));
        modelo.addAttribute("siguientes", siguientes(d.path("fase").asText("REGISTRO")));
        return "desastres/tablero";
    }

    @PostMapping("/desastres/{ref}/fase")
    public String fase(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable String ref,
                       @RequestParam long versionBase, @RequestParam String fase, @RequestParam String motivo,
                       RedirectAttributes flash) {
        return Acciones.ejecutar(flash, "/desastres/" + ref, "La fase cambió a " + fase + ".",
                () -> nucleo.cambiarFase(sesion.token(), ref, versionBase, fase, motivo));
    }

    @PostMapping("/desastres/{ref}/caracterizacion")
    public String caracterizar(@RequestAttribute(Sesion.ATRIBUTO) Sesion sesion, @PathVariable String ref,
                               @RequestParam long versionBase, @RequestParam double latitud,
                               @RequestParam double longitud, @RequestParam double radioKm,
                               @RequestParam(required = false) Integer nivel,
                               @RequestParam(required = false) String descripcion, RedirectAttributes flash) {
        return Acciones.ejecutar(flash, "/desastres/" + ref, "Caracterización guardada.",
                () -> nucleo.caracterizar(sesion.token(), ref, versionBase, latitud, longitud, radioKm, nivel, descripcion));
    }

    /** Fases a las que el coordinador puede llevar el desastre desde aquí (Reconstrucción se inicia en su módulo). */
    static List<String> siguientes(String fase) {
        return switch (fase) {
            case "REGISTRO" -> List.of("CARACTERIZACION", "CIERRE");
            case "CARACTERIZACION" -> List.of("CIERRE");
            default -> List.of();
        };
    }
}
