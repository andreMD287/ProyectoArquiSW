# Borrador para el SAD (arc42 §9 y §7) · ADR-23 a ADR-29 y ambiente entregado

> Borrador interno de Rol 1 (15-nov-2026) para pasar al SAD en arc42. Mismo formato que ADR-01 a 22.
> Las cifras son las de `evidencias/rol1-tablero/medicion_tablero_2026-11-15_1334.json` y `evidencias/rol1-build/`.
> ADR-25 y ADR-27 los completa Rol 2 (mensaje M6).

### ADR-23 — Una sola presentación web MVC con Spring MVC y Thymeleaf

- **Requisitos.** Restricción del curso (presentación MVC con un framework); QA-SEG-01, QA-ESC-01; mínimos a–d.
- **Contexto.** Había tres tecnologías de interfaz: HTML estático en `servidor/web`, React en Reconstrucción y la app MAUI. Ninguna de las dos interfaces web usaba un framework MVC, y cada una manejaba la sesión por su cuenta en el navegador.
- **Alternativas.**
  - JSF (Taller 4): MVC por componentes, con estado de vista por sesión.
  - ASP.NET Core MVC (TallerNetCore): MVC sin servlets.
  - Spring MVC + Thymeleaf.
  - Dejar HTML estático y React.
- **Decisión.** Unidad nueva UD-09 Portal web (`portal/`), en Spring Boot 3.3 con Spring MVC y Thymeleaf. Es la única interfaz web.
  - Controlador: clases `@Controller`, con el `DispatcherServlet` como controlador frontal.
  - Modelo: los datos que entregan los servicios dueños.
  - Vista: plantillas Thymeleaf.
  - Cómo habla con cada servicio: con el núcleo (desastres) y con `servidor` (evaluaciones) por REST; con Reconstrucción por gRPC.
- **Justificación.**
  - Un solo inicio de sesión y una sola entrada.
  - El token del núcleo queda en la sesión del servidor: el navegador solo tiene una cookie HttpOnly, sin el id de sesión en la URL.
  - Los controladores no guardan estado de vista, a diferencia de JSF, lo que favorece el escenario B.
  - Corre en un contenedor de servlets, lo que hace posible ADR-24.
  - No agrega tecnología: Reconstrucción ya usa Spring Boot 3.3.
- **Consecuencias.**
  - A favor: se verifica de punta a punta (`verificar_portal.py`, 43 comprobaciones con tres roles); gRPC tiene por fin un cliente real.
  - En contra: el portal depende de los tres servicios y repite algunos textos de validación; la copia del contrato gRPC (`portal/src/main/proto`) debe mantenerse igual a la de Reconstrucción.

### ADR-24 — Servlet asíncrono y multihilo para el tablero de un desastre

- **Requisitos.** Restricción del curso (multihilo con servlets); QA-REN-01, QA-RES-02, QA-ESC-01; mínimo c.
- **Contexto.** El tablero de un desastre junta datos de tres dueños distintos: el desastre (núcleo), sus evaluaciones (`servidor`) y sus proyectos (Reconstrucción). Consultarlos uno tras otro suma sus latencias, y si uno falla se pierde toda la página.
- **Alternativas.**
  - Consultas secuenciales en el controlador.
  - Consultas paralelas desde el navegador.
  - Un servlet asíncrono con un pool acotado.
- **Decisión.** `TableroServlet`, con `@WebServlet(asyncSupported = true)` en `GET /tablero`, funciona así:
  1. `startAsync` libera el hilo de Tomcat.
  2. Un `ThreadPoolExecutor` de 16 hilos con cola de 64 lanza las tres consultas en paralelo.
  3. Cada fuente tiene su límite de 1,5 s, y la respuesta completa tiene un límite de 3 s.
  4. Si una fuente falla, se responde con las demás y se indica `fuentesNoDisponibles`.
  5. Con el pool y la cola llenos, responde 503 con `Retry-After`.
- **Justificación.**
  - Sin carga, la latencia es la de la fuente más lenta: con un cliente, p50 de 36 ms y p95 de 56 ms, frente a 46 y 67 ms en secuencial.
  - Una fuente caída no tumba el tablero: con Reconstrucción congelada, 60 de 60 respuestas fueron parciales, con p95 de 1,52 s y ningún error.
  - El pool acotado pone un límite a la presión sobre los servicios de atrás.
- **Consecuencias.**
  - **Trade-off medido:** con 20 clientes en una sola máquina, el paralelo es un 12 % más lento que el secuencial (p95 de 323 ms frente a 288 ms). Al estar saturados los servicios de atrás, triplicar la concurrencia sobre ellos no ayuda, y todos validan el token contra el núcleo. Con servicios escalados por separado, como en el despliegue objetivo, se espera la ganancia de la fila sin carga.
  - **Puntos de sensibilidad:** el tamaño del pool, la cola y el límite por fuente.
  - **Plazo de los clientes del tablero:** se fijó igual al límite de la fuente. Con el plazo de 5 s de los clientes de las páginas, una fuente colgada agotaba el pool: 25 de 60 respuestas fueron 503 en la primera medición.

### ADR-25 — Operación sin conexión con .NET MAUI y SQLite *(texto de Rol 2, mensaje M6)*

- **Requisitos.** Restricción del curso (componentes .NET para desconectados); QA-DIS-03, QA-CNF-01; mínimo a.
- **Decisión (resumen).** La app de campo es .NET MAUI con SQLite como fuente de verdad local. Sincroniza por lotes idempotentes al recuperar la red y muestra los conflictos de versión sin sobrescribirlos.
- *Contexto, alternativas (Flutter del TallerNetCore), justificación y consecuencias: los escribe Rol 2.*

### ADR-26 — Reconstrucción física en Spring Boot con REST y gRPC interno

- **Requisitos.** Mínimo b; QA-INT-01, QA-TRZ-01; CON-02.
- **Contexto.**
  - El diseño del taller de Reconstrucción era .NET.
  - Por la nota de clase, .NET queda para lo desconectado (campo).
  - Reconstrucción es un *back-office* en línea que comparte con el núcleo la identidad y la fase del desastre.
- **Alternativas.** .NET con gRPC y SignalR (el taller); Jakarta EE junto al núcleo; Spring Boot aparte.
- **Decisión.**
  - Unidad propia en Spring Boot 3.3 con su base PostgreSQL.
  - REST para clientes heterogéneos y scripts; gRPC (13 + 1 RPC) como canal interno, que usa el portal.
  - Valida el token contra el núcleo en un filtro (REST) y en un interceptor (gRPC).
  - Bandeja de salida hacia RabbitMQ, con el formato de aviso del contrato C5.
- **Justificación.**
  - Mismo lenguaje que el núcleo y el portal para el back-office en línea.
  - Las reglas viven en el dominio, sin dependencias del framework (RN-01 a RN-20), y se prueban en memoria: 31 pruebas.
  - gRPC da un contrato tipado y eficiente entre servicios Java.
- **Consecuencias.**
  - Una tercera base de datos y un tercer contenedor de aplicación.
  - El tiempo real del diseño (SignalR) se reemplaza por el refresco del tablero.
  - Solo maneja activos físicos: no guarda datos de personas.

### ADR-27 — Consumo continuo de RabbitMQ en notificaciones *(texto de Rol 2, mensaje M6)*

### ADR-28 — Personas afectadas, recursos y donaciones como sistemas externos

- **Requisitos.** Enunciado (sistemas existentes de gestión de recursos y de atención de personas); indicación del profesor en clase.
- **Decisión.**
  - La plataforma no implementa estas capacidades: quedan como sistemas externos (SE) con su punto de integración.
  - Se retira la UD-05.
  - Las Solicitudes de recursos del Taller 2 salen del portal, y su código se retirará del núcleo y de `servidor`.
  - Reconstrucción no registra personas ni número de afectados.
- **Consecuencias.** Menos alcance y menos documento; RF-PA y RF-GR pasan a "fuera de alcance".

### ADR-29 — Metas de rendimiento con percentiles medidos

- **Requisitos.** QA-REN-01..03; anotación del profesor sobre los escenarios.
- **Decisión.**
  - Las medidas de respuesta se expresan como p50 y p95, siguiendo a Bass, Clements y Kazman (latencia más tasa de incumplimiento) y el libro de SRE de Google (distribuciones, no promedios).
  - Se miden en el ambiente entregado, indicando el número de solicitudes y la concurrencia.
  - Las metas del despliegue de tres centros de datos se presentan como metas de diseño.
- **Evidencia.** `portal/scripts/medir_tablero.py` y su informe JSON.

---

## Ambiente entregado (arc42 §7.1): Docker Compose en una máquina

| Contenedor | Unidad | Tecnología | Puerto en el equipo | Habla con |
| --- | --- | --- | --- | --- |
| `web` | UD-01 Entrada | Nginx 1.28 | 127.0.0.1:18085 | `portal` (`/`), `api` (`/api/`), `reconstruccion` (`/api/reconstruccion/`) |
| `portal` | UD-09 Portal web | Spring Boot 3.3, Spring MVC, Thymeleaf, Tomcat | — | `nucleo` (REST), `api` (REST), `reconstruccion` (gRPC 9096) |
| `nucleo` | UD-02 Núcleo | Jakarta EE en WildFly 41 (EJB, JPA, JTA, JAX-RS, SOAP, EJB remoto) | 127.0.0.1:18092 (pruebas) | `database`, `desastres` (HTTP, bandeja de salida) |
| `database` | Base del núcleo | PostgreSQL 17 | — | — |
| `api` | UD-03 Campo y mapas | ASP.NET Core 10 | — | `db`, `nucleo` (REST: sesión y catálogo), `desastres` |
| `db` | Base de `servidor` | PostgreSQL 17 | — | — |
| `desastres` | UD-04 Notificaciones | Jakarta EE en Payara 6 | — | `rabbitmq` (AMQP), `mailpit` (SMTP) |
| `rabbitmq` | Mensajería | RabbitMQ 4.1 | 127.0.0.1:15683–15684 | — |
| `mailpit` | Buzón de pruebas | Mailpit | 127.0.0.1:18026 | — |
| `reconstruccion` | UD-06 Reconstrucción | Spring Boot 3.3 (REST 8086, gRPC 9096) | 127.0.0.1:19096 (gRPC) | `reconstruccion-db`, `nucleo` (sesión y fase), `api` (evaluaciones), `rabbitmq` |
| `reconstruccion-db` | Base de Reconstrucción | PostgreSQL 17 | — | — |
| Teléfono Android | UD-03 (cliente) | .NET MAUI + SQLite | — | `web` → `api` (`/api/campo/lotes`) |

Secretos: `servidor/.env`, que no se versiona y se crea desde `.env.ejemplo`. Inicio: `./iniciar.sh`. Verificación: `./verificar.sh` (243 comprobaciones el 15-nov).
