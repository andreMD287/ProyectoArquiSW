package com.pgdr.tablero.config;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.pgdr.tablero.cliente.ClienteHttp;
import com.pgdr.tablero.cliente.NucleoCliente;
import com.pgdr.tablero.cliente.ReconstruccionCliente;
import com.pgdr.tablero.cliente.ServidorCliente;
import com.pgdr.tablero.composicion.FuentesTablero;
import com.pgdr.tablero.composicion.Tablero;
import io.grpc.ManagedChannel;
import io.grpc.ManagedChannelBuilder;
import java.time.Duration;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.web.client.ClientHttpRequestFactories;
import org.springframework.boot.web.client.ClientHttpRequestFactorySettings;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.client.JdkClientHttpRequestFactory;
import org.springframework.web.client.RestClient;

@Configuration
public class ConfiguracionTablero {

    /**
     * Cliente REST cuyo plazo de lectura es el límite de la fuente: si un servicio se cuelga, el
     * hilo del pool se libera al vencer ese límite. Con un plazo mayor, una fuente colgada agotaba
     * el pool (medición del 15-nov: 25 de 60 respuestas fueron 503).
     */
    private static RestClient rest(String url, Duration lectura) {
        ClientHttpRequestFactorySettings limites = ClientHttpRequestFactorySettings.DEFAULTS
                .withConnectTimeout(Duration.ofSeconds(2)).withReadTimeout(lectura);
        // Cliente HTTP del JDK: a diferencia de HttpURLConnection, entrega bien las respuestas 401.
        return RestClient.builder().baseUrl(url)
                .requestFactory(ClientHttpRequestFactories.get(JdkClientHttpRequestFactory.class, limites)).build();
    }

    @Bean(destroyMethod = "shutdownNow")
    ManagedChannel canalReconstruccion(@Value("${tablero.reconstruccion.host}") String host,
                                       @Value("${tablero.reconstruccion.puerto}") int puerto) {
        ManagedChannel canal = ManagedChannelBuilder.forAddress(host, puerto).usePlaintext().build();
        canal.getState(true);   // conecta desde el arranque: la primera consulta del tablero no paga la conexión
        return canal;
    }

    /**
     * Pool acotado del tablero (ADR-24): hilos fijos y cola limitada. Con todo lleno rechaza
     * (AbortPolicy) y el servlet responde 503, en vez de acumular trabajo sin límite.
     */
    @Bean(destroyMethod = "shutdown")
    ExecutorService poolTablero(@Value("${tablero.hilos}") int hilos, @Value("${tablero.cola}") int cola) {
        AtomicInteger numero = new AtomicInteger();
        return new ThreadPoolExecutor(hilos, hilos, 30, TimeUnit.SECONDS, new ArrayBlockingQueue<>(cola), r -> {
            Thread t = new Thread(r, "tablero-" + numero.incrementAndGet());
            t.setDaemon(true);
            return t;
        }, new ThreadPoolExecutor.AbortPolicy());
    }

    @Bean
    Tablero tablero(ExecutorService poolTablero, @Value("${tablero.limite-fuente-ms}") long limiteFuenteMs) {
        return new Tablero(poolTablero, limiteFuenteMs);
    }

    @Bean
    FuentesTablero fuentesTablero(@Value("${tablero.nucleo.url}") String nucleoUrl,
                                  @Value("${tablero.servidor.url}") String servidorUrl,
                                  ManagedChannel canalReconstruccion, ObjectMapper json,
                                  @Value("${tablero.limite-fuente-ms}") long limiteFuenteMs) {
        Duration plazo = Duration.ofMillis(limiteFuenteMs);
        return new FuentesTablero(
                new NucleoCliente(new ClienteHttp(rest(nucleoUrl, plazo), json, "de identidad y desastres (núcleo)")),
                new ServidorCliente(new ClienteHttp(rest(servidorUrl, plazo), json, "de evaluaciones (servidor)")),
                new ReconstruccionCliente(canalReconstruccion, limiteFuenteMs));
    }
}
