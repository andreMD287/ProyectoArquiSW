package com.pgdr.portal.cliente;

import com.pgdr.reconstruccion.grpc.AdjuntarEvidenciaRequest;
import com.pgdr.reconstruccion.grpc.AprobarProyectoRequest;
import com.pgdr.reconstruccion.grpc.AuditoriaResponse;
import com.pgdr.reconstruccion.grpc.CambiarEstadoRequest;
import com.pgdr.reconstruccion.grpc.ConsultarAuditoriaRequest;
import com.pgdr.reconstruccion.grpc.CrearProyectoRequest;
import com.pgdr.reconstruccion.grpc.DesastreMsg;
import com.pgdr.reconstruccion.grpc.DesastreRequest;
import com.pgdr.reconstruccion.grpc.EvaluacionesResponse;
import com.pgdr.reconstruccion.grpc.IniciarEjecucionRequest;
import com.pgdr.reconstruccion.grpc.ModificarPresupuestoRequest;
import com.pgdr.reconstruccion.grpc.NuevoProyecto;
import com.pgdr.reconstruccion.grpc.ProyectoMsg;
import com.pgdr.reconstruccion.grpc.ReconstruccionServiceGrpc;
import com.pgdr.reconstruccion.grpc.RegistrarAvanceRequest;
import com.pgdr.reconstruccion.grpc.RegistrarCostoRequest;
import com.pgdr.reconstruccion.grpc.SeguimientoResponse;
import io.grpc.ManagedChannel;
import io.grpc.Metadata;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import io.grpc.stub.MetadataUtils;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.TimeUnit;
import java.util.function.Function;

/**
 * Reconstrucción (UD-06) por gRPC: el canal interno entre servicios Java, con contrato tipado
 * (reconstruccion.proto). El token del usuario viaja en el metadato "authorization" y
 * Reconstrucción lo valida contra el núcleo; la identidad del mensaje se ignora.
 */
public class ReconstruccionCliente {
    private static final Metadata.Key<String> AUTORIZACION =
            Metadata.Key.of("authorization", Metadata.ASCII_STRING_MARSHALLER);

    private final ManagedChannel canal;
    private final long plazoMs;

    public ReconstruccionCliente(ManagedChannel canal, long plazoMs) {
        this.canal = canal;
        this.plazoMs = plazoMs;
    }

    /** Reconstrucción identifica el desastre con un UUID derivado de su referencia en el núcleo. */
    public static UUID desastreId(String referencia) {
        return UUID.nameUUIDFromBytes(("desastre:" + referencia).getBytes(StandardCharsets.UTF_8));
    }

    public SeguimientoResponse seguimiento(String token, String ref) {
        return llamar(token, s -> s.consultarSeguimiento(desastre(ref)));
    }

    public EvaluacionesResponse evaluaciones(String token, String ref) {
        return llamar(token, s -> s.consultarEvaluaciones(desastre(ref)));
    }

    public DesastreMsg iniciarReconstruccion(String token, String ref) {
        return llamar(token, s -> s.iniciarReconstruccion(desastre(ref)));
    }

    public DesastreMsg cerrar(String token, String ref) {
        return llamar(token, s -> s.cerrarDesastre(desastre(ref)));
    }

    public ProyectoMsg crear(String token, String ref, String nombre, String tipo, List<String> evaluaciones) {
        NuevoProyecto proyecto = NuevoProyecto.newBuilder().setNombre(nombre).setTipo(tipo == null ? "" : tipo)
                .addAllEvaluaciones(evaluaciones).build();
        return llamar(token, s -> s.crearProyecto(CrearProyectoRequest.newBuilder()
                .setDesastreId(desastreId(ref).toString()).setProyecto(proyecto).build()));
    }

    public ProyectoMsg aprobar(String token, String proyectoId, String presupuesto, String fuente, String responsable, Long version) {
        var b = AprobarProyectoRequest.newBuilder().setProyectoId(proyectoId)
                .setPresupuesto(presupuesto).setFuenteFinanciacion(fuente).setResponsable(responsable);
        if (version != null) b.setVersionEsperada(version);
        return llamar(token, s -> s.aprobarProyecto(b.build()));
    }

    public ProyectoMsg iniciarEjecucion(String token, String proyectoId, String contratista, String inicio, String fin, Long version) {
        var b = IniciarEjecucionRequest.newBuilder().setProyectoId(proyectoId)
                .setContratista(contratista).setInicio(inicio).setFin(fin);
        if (version != null) b.setVersionEsperada(version);
        return llamar(token, s -> s.iniciarEjecucion(b.build()));
    }

    public ProyectoMsg avance(String token, String proyectoId, int porcentaje, String descripcion, String motivo, Long version) {
        var b = RegistrarAvanceRequest.newBuilder().setProyectoId(proyectoId)
                .setPorcentaje(porcentaje).setDescripcion(texto(descripcion)).setMotivoCorreccion(texto(motivo));
        if (version != null) b.setVersionEsperada(version);
        return llamar(token, s -> s.registrarAvance(b.build()));
    }

    public ProyectoMsg costo(String token, String proyectoId, String valor, String descripcion, Long version) {
        var b = RegistrarCostoRequest.newBuilder().setProyectoId(proyectoId).setValor(valor).setDescripcion(texto(descripcion));
        if (version != null) b.setVersionEsperada(version);
        return llamar(token, s -> s.registrarCosto(b.build()));
    }

    public ProyectoMsg evidencia(String token, String proyectoId, String descripcion, Long version) {
        var b = AdjuntarEvidenciaRequest.newBuilder().setProyectoId(proyectoId).setDescripcion(texto(descripcion));
        if (version != null) b.setVersionEsperada(version);
        return llamar(token, s -> s.adjuntarEvidencia(b.build()));
    }

    public ProyectoMsg presupuesto(String token, String proyectoId, String valor, String motivo, Long version) {
        var b = ModificarPresupuestoRequest.newBuilder().setProyectoId(proyectoId).setPresupuesto(valor).setMotivo(texto(motivo));
        if (version != null) b.setVersionEsperada(version);
        return llamar(token, s -> s.modificarPresupuesto(b.build()));
    }

    public ProyectoMsg estado(String token, String proyectoId, String estado, String motivo, Long version) {
        var b = CambiarEstadoRequest.newBuilder().setProyectoId(proyectoId).setEstado(estado).setMotivo(texto(motivo));
        if (version != null) b.setVersionEsperada(version);
        return llamar(token, s -> s.cambiarEstado(b.build()));
    }

    public AuditoriaResponse auditoria(String token, String proyectoId) {
        return llamar(token, s -> s.consultarAuditoria(ConsultarAuditoriaRequest.newBuilder().setProyectoId(proyectoId).build()));
    }

    private static DesastreRequest desastre(String ref) {
        return DesastreRequest.newBuilder().setDesastreId(desastreId(ref).toString()).build();
    }

    private <T> T llamar(String token, Function<ReconstruccionServiceGrpc.ReconstruccionServiceBlockingStub, T> rpc) {
        Metadata metadatos = new Metadata();
        metadatos.put(AUTORIZACION, "Bearer " + token);
        ReconstruccionServiceGrpc.ReconstruccionServiceBlockingStub stub = ReconstruccionServiceGrpc.newBlockingStub(canal)
                .withInterceptors(MetadataUtils.newAttachHeadersInterceptor(metadatos))
                .withDeadlineAfter(plazoMs, TimeUnit.MILLISECONDS);
        try {
            return rpc.apply(stub);
        } catch (StatusRuntimeException e) {
            throw traducir(e);
        }
    }

    /** Los códigos gRPC de Reconstrucción, como errores del portal. */
    static ErrorServicio traducir(StatusRuntimeException e) {
        Status.Code codigo = e.getStatus().getCode();
        String descripcion = e.getStatus().getDescription() == null ? "" : e.getStatus().getDescription();
        String regla = null;
        String mensaje = descripcion;
        if (descripcion.matches("RN-\\d+:.*")) {
            regla = descripcion.substring(0, descripcion.indexOf(':'));
            mensaje = descripcion.substring(descripcion.indexOf(':') + 1).trim();
        }
        int estado = switch (codigo) {
            case FAILED_PRECONDITION -> 422;
            case PERMISSION_DENIED -> 403;
            case UNAUTHENTICATED -> 401;
            case NOT_FOUND -> 404;
            case ABORTED -> 409;
            case INVALID_ARGUMENT -> 400;
            case DEADLINE_EXCEEDED, UNAVAILABLE -> 503;
            default -> 500;
        };
        if (mensaje.isBlank())
            mensaje = estado == 503 ? "Reconstrucción no responde en este momento." : "Reconstrucción respondió " + codigo + ".";
        return new ErrorServicio(estado, mensaje, regla);
    }

    private static String texto(String s) {
        return s == null ? "" : s;
    }
}
