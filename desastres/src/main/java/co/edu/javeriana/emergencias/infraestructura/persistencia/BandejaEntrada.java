package co.edu.javeriana.emergencias.infraestructura.persistencia;

import jakarta.annotation.PostConstruct;
import jakarta.ejb.*;
import java.nio.channels.FileChannel;
import java.nio.file.*;
import java.time.Instant;
import java.util.*;

/**
 * Bandeja de entrada de los avisos de Reconstrucción (contrato C5). Guarda el estado de cada
 * idEvento en un archivo del volumen, así un reinicio no olvida lo ya enviado:
 * RECIBIDO → ENVIADO, SIN_CORREO (tipo que no lleva correo), PENDIENTE (falló y se reintenta)
 * o FALLIDO (agotó los reintentos y quedó en la cola de errores).
 * Un único escritor (Singleton con bloqueo de escritura); escritura atómica con archivo temporal.
 */
@Singleton
@LocalBean
@TransactionAttribute(TransactionAttributeType.NOT_SUPPORTED)
public class BandejaEntrada {
    public static final int MAX_INTENTOS = 5;

    public static class Entrada {
        public String tipo;
        public String estado;
        public int intentos;
        public String recibidoEn;
        public String actualizadoEn;
        public String detalle;
    }

    public static class Contenido {
        public Map<String, Entrada> eventos = new LinkedHashMap<>();
    }

    private Path archivo;
    private Contenido contenido;

    public BandejaEntrada() { }

    public BandejaEntrada(Path archivo) { this.archivo = archivo; cargar(); }

    @PostConstruct
    public void iniciar() {
        if (archivo == null) archivo = Path.of(System.getenv().getOrDefault("BANDEJA_ENTRADA_ARCHIVO",
                "/opt/payara/registro/bandeja-entrada.json"));
        cargar();
    }

    private void cargar() {
        try {
            Files.createDirectories(archivo.getParent());
            contenido = Files.exists(archivo) ? Json.leer(Files.readString(archivo), Contenido.class) : new Contenido();
            if (contenido == null || contenido.eventos == null) contenido = new Contenido();
        } catch (Exception e) { throw new IllegalStateException("No se pudo abrir la bandeja de entrada", e); }
    }

    /** true si el aviso ya se atendió (ENVIADO o SIN_CORREO): el repetido se descarta. */
    public boolean yaAtendido(String idEvento) {
        Entrada e = contenido.eventos.get(idEvento);
        return e != null && ("ENVIADO".equals(e.estado) || "SIN_CORREO".equals(e.estado));
    }

    /** Registra un intento de proceso (RECIBIDO) y devuelve el número de intento. */
    public int recibir(String idEvento, String tipo) {
        Entrada e = contenido.eventos.computeIfAbsent(idEvento, k -> nueva(tipo));
        e.intentos++;
        e.estado = "RECIBIDO";
        e.actualizadoEn = Instant.now().toString();
        guardar();
        return e.intentos;
    }

    public void marcar(String idEvento, String estado, String detalle) {
        Entrada e = contenido.eventos.computeIfAbsent(idEvento, k -> nueva(null));
        e.estado = estado;
        e.detalle = detalle;
        e.actualizadoEn = Instant.now().toString();
        guardar();
    }

    public Optional<Entrada> buscar(String idEvento) { return Optional.ofNullable(contenido.eventos.get(idEvento)); }

    private static Entrada nueva(String tipo) {
        Entrada e = new Entrada();
        e.tipo = tipo;
        e.recibidoEn = Instant.now().toString();
        return e;
    }

    private void guardar() {
        Path temporal = archivo.resolveSibling(archivo.getFileName() + ".tmp");
        try {
            Files.writeString(temporal, Json.escribir(contenido));
            try (var canal = FileChannel.open(temporal, StandardOpenOption.WRITE)) { canal.force(true); }
            Files.move(temporal, archivo, StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
        } catch (Exception e) { throw new IllegalStateException("No se pudo guardar la bandeja de entrada", e); }
    }
}
