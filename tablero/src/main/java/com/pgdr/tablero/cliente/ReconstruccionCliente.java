package com.pgdr.tablero.cliente;

import com.pgdr.reconstruccion.grpc.DesastreRequest;
import com.pgdr.reconstruccion.grpc.ReconstruccionServiceGrpc;
import com.pgdr.reconstruccion.grpc.SeguimientoResponse;
import io.grpc.ManagedChannel;
import io.grpc.Metadata;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import io.grpc.stub.MetadataUtils;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.concurrent.TimeUnit;

/**
 * Reconstrucción (UD-06) por gRPC (contrato reconstruccion.proto). El token del usuario viaja en
 * el metadato "authorization" y Reconstrucción lo valida contra el núcleo.
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
        Metadata metadatos = new Metadata();
        metadatos.put(AUTORIZACION, "Bearer " + token);
        try {
            return ReconstruccionServiceGrpc.newBlockingStub(canal)
                    .withInterceptors(MetadataUtils.newAttachHeadersInterceptor(metadatos))
                    .withDeadlineAfter(plazoMs, TimeUnit.MILLISECONDS)
                    .consultarSeguimiento(DesastreRequest.newBuilder().setDesastreId(desastreId(ref).toString()).build());
        } catch (StatusRuntimeException e) {
            throw traducir(e);
        }
    }

    /** Los códigos gRPC de Reconstrucción, como estados HTTP equivalentes. */
    static ErrorServicio traducir(StatusRuntimeException e) {
        Status.Code codigo = e.getStatus().getCode();
        String mensaje = e.getStatus().getDescription() == null ? "" : e.getStatus().getDescription();
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
        return new ErrorServicio(estado, mensaje, null);
    }
}
