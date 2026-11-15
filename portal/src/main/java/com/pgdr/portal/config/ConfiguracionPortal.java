package com.pgdr.portal.config;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.pgdr.portal.cliente.ClienteHttp;
import com.pgdr.portal.cliente.NucleoCliente;
import com.pgdr.portal.cliente.ReconstruccionCliente;
import com.pgdr.portal.cliente.ServidorCliente;
import com.pgdr.portal.sesion.InterceptorSesion;
import com.pgdr.portal.tablero.FuentesTablero;
import com.pgdr.portal.tablero.Tablero;
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
import org.springframework.web.servlet.config.annotation.InterceptorRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;

@Configuration
public class ConfiguracionPortal implements WebMvcConfigurer {

    @Override
    public void addInterceptors(InterceptorRegistry registro) {
        registro.addInterceptor(new InterceptorSesion()).addPathPatterns("/**")
                .excludePathPatterns("/login", "/salud", "/error", "/css/**", "/js/**", "/webjars/**", "/favicon.ico");
    }

    private static RestClient rest(String url) {
        return rest(url, Duration.ofSeconds(5));
    }

    private static RestClient rest(String url, Duration lectura) {
        // Límites cortos: si un servicio no responde, el portal lo dice en vez de quedarse esperando.
        ClientHttpRequestFactorySettings limites = ClientHttpRequestFactorySettings.DEFAULTS
                .withConnectTimeout(Duration.ofSeconds(2)).withReadTimeout(lectura);
        // Cliente HTTP del JDK: a diferencia de HttpURLConnection, entrega bien las respuestas 401.
        return RestClient.builder().baseUrl(url)
                .requestFactory(ClientHttpRequestFactories.get(JdkClientHttpRequestFactory.class, limites)).build();
    }

    @Bean
    NucleoCliente nucleoCliente(@Value("${portal.nucleo.url}") String url, ObjectMapper json) {
        return new NucleoCliente(new ClienteHttp(rest(url), json, "de identidad y desastres (núcleo)"));
    }

    @Bean
    ServidorCliente servidorCliente(@Value("${portal.servidor.url}") String url, ObjectMapper json) {
        return new ServidorCliente(new ClienteHttp(rest(url), json, "de evaluaciones (servidor)"));
    }

    @Bean(destroyMethod = "shutdownNow")
    ManagedChannel canalReconstruccion(@Value("${portal.reconstruccion.host}") String host,
                                       @Value("${portal.reconstruccion.puerto}") int puerto) {
        return ManagedChannelBuilder.forAddress(host, puerto).usePlaintext().build();
    }

    @Bean
    ReconstruccionCliente reconstruccionCliente(ManagedChannel canalReconstruccion) {
        return new ReconstruccionCliente(canalReconstruccion, 5000);
    }

    /**
     * Pool acotado del tablero (ADR-24): hilos fijos y cola limitada. Con todo lleno rechaza
     * (AbortPolicy) y el servlet responde 503, en vez de acumular trabajo sin límite.
     */
    @Bean(destroyMethod = "shutdown")
    ExecutorService poolTablero(@Value("${portal.tablero.hilos}") int hilos, @Value("${portal.tablero.cola}") int cola) {
        AtomicInteger numero = new AtomicInteger();
        return new ThreadPoolExecutor(hilos, hilos, 30, TimeUnit.SECONDS, new ArrayBlockingQueue<>(cola), r -> {
            Thread t = new Thread(r, "tablero-" + numero.incrementAndGet());
            t.setDaemon(true);
            return t;
        }, new ThreadPoolExecutor.AbortPolicy());
    }

    @Bean
    Tablero tablero(ExecutorService poolTablero, @Value("${portal.tablero.limite-fuente-ms}") long limiteFuenteMs) {
        return new Tablero(poolTablero, limiteFuenteMs);
    }

    /**
     * Las fuentes del tablero usan clientes propios cuyo plazo es el límite de la fuente: si un
     * servicio se cuelga, el hilo del pool se libera al vencer ese límite y no a los 5 s de los
     * clientes de las páginas. Así una fuente caída no agota el pool (medición del 15-nov).
     */
    @Bean
    FuentesTablero fuentesTablero(@Value("${portal.nucleo.url}") String nucleoUrl,
                                  @Value("${portal.servidor.url}") String servidorUrl,
                                  ManagedChannel canalReconstruccion, ObjectMapper json,
                                  @Value("${portal.tablero.limite-fuente-ms}") long limiteFuenteMs) {
        Duration plazo = Duration.ofMillis(limiteFuenteMs);
        return new FuentesTablero(
                new NucleoCliente(new ClienteHttp(rest(nucleoUrl, plazo), json, "de identidad y desastres (núcleo)")),
                new ServidorCliente(new ClienteHttp(rest(servidorUrl, plazo), json, "de evaluaciones (servidor)")),
                new ReconstruccionCliente(canalReconstruccion, limiteFuenteMs));
    }
}
