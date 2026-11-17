package com.pgdr.tablero.composicion;

import com.fasterxml.jackson.databind.JsonNode;
import com.pgdr.tablero.cliente.NucleoCliente;
import com.pgdr.tablero.cliente.ReconstruccionCliente;
import com.pgdr.tablero.cliente.ServidorCliente;
import com.pgdr.reconstruccion.grpc.ProyectoMsg;
import com.pgdr.reconstruccion.grpc.SeguimientoResponse;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;

/** Liga las tres fuentes del tablero a los clientes de servicio y les da la forma que usa el mapa. */
public class FuentesTablero {
    private final NucleoCliente nucleo;
    private final ServidorCliente servidor;
    private final ReconstruccionCliente reconstruccion;

    public FuentesTablero(NucleoCliente nucleo, ServidorCliente servidor, ReconstruccionCliente reconstruccion) {
        this.nucleo = nucleo;
        this.servidor = servidor;
        this.reconstruccion = reconstruccion;
    }

    public Tablero.Consultas para(String token, String ref) {
        return new Tablero.Consultas(() -> nucleo.desastre(token, ref), () -> evaluaciones(token, ref),
                () -> proyectos(token, ref));
    }

    private Map<String, Object> evaluaciones(String token, String ref) {
        JsonNode puntos = servidor.evaluaciones(token, ref);
        List<Map<String, Object>> lista = new ArrayList<>();
        Map<String, Integer> porNivel = new TreeMap<>();
        Map<String, Integer> porActivo = new TreeMap<>();
        for (JsonNode p : puntos) {
            Map<String, Object> e = new LinkedHashMap<>();
            e.put("id", p.path("id").asText());
            e.put("latitud", p.path("latitud").asDouble());
            e.put("longitud", p.path("longitud").asDouble());
            e.put("nivel", p.path("nivelAfectacion").isNumber() ? p.path("nivelAfectacion").asInt() : null);
            e.put("activo", p.path("activo").asText(""));
            e.put("tipoActivo", p.path("tipoActivo").asText(""));
            e.put("descripcion", p.path("descripcion").asText(""));
            e.put("registradaEn", p.path("registradaEn").asText(""));
            lista.add(e);
            if (e.get("nivel") != null) porNivel.merge(String.valueOf(e.get("nivel")), 1, Integer::sum);
            // Contrato C1: se agrupa por tipo de activo (Puente, Escuela…); sin tipo, por el texto libre.
            String tipo = p.path("tipoActivoNombre").asText("");
            String activo = !tipo.isBlank() ? tipo : e.get("activo").toString().isBlank() ? "Sin tipo" : e.get("activo").toString();
            porActivo.merge(activo, 1, Integer::sum);
        }
        Map<String, Object> r = new LinkedHashMap<>();
        r.put("total", lista.size());
        r.put("porNivel", porNivel);
        r.put("porActivo", porActivo);
        r.put("puntos", lista);
        return r;
    }

    private Map<String, Object> proyectos(String token, String ref) {
        SeguimientoResponse s = reconstruccion.seguimiento(token, ref);
        List<Map<String, Object>> lista = new ArrayList<>();
        for (ProyectoMsg p : s.getProyectosList()) {
            Map<String, Object> m = new LinkedHashMap<>();
            m.put("id", p.getId());
            m.put("nombre", p.getNombre());
            m.put("estado", p.getEstado());
            m.put("tipoActivo", p.getTipoActivo());
            m.put("avance", p.getAvance());
            m.put("presupuesto", p.getPresupuesto());
            m.put("sobrecosto", p.getSobrecosto());
            m.put("evaluaciones", p.getEvaluacionesList());
            lista.add(m);
        }
        Map<String, Object> r = new LinkedHashMap<>();
        r.put("estadoDesastre", s.getDesastre().getEstado());
        r.put("avancePonderado", s.getAvancePonderado());
        r.put("presupuestoTotal", s.getPresupuestoTotal());
        r.put("costoTotal", s.getCostoTotal());
        r.put("conSobrecosto", s.getConSobrecosto());
        r.put("porEstado", s.getPorEstadoMap());
        r.put("proyectos", lista);
        return r;
    }
}
