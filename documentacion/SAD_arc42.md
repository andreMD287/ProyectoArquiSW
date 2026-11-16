# SAD v2.0 · Plataforma para la Gestión de Desastres y Reconstrucción

**Documento de arquitectura de software, organizado según arc42**

**Versión:** 2.0 · **Fecha:** 15 de noviembre de 2026

**Equipo:** Andrés Barrios, Nicolás Parra, Thomás López, André Landinez

**Pontificia Universidad Javeriana** · Arquitectura de Software

## Control de versiones

| Versión | Fecha | Cambios |
| --- | --- | --- |
| Primera entrega | 19-sep-2026 | Diseño inicial: un núcleo con tres satélites |
| 1.0 | 10-nov-2026 | Ocho unidades de despliegue, vistas 4+1 y 22 decisiones. Responde al SRS v1.0 |
| 2.0 | 15-nov-2026 | Reorganizado según arc42. Agrega el portal web MVC (UD-09) con su servlet multihilo y registra Reconstrucción (UD-06) como construida en Spring Boot con REST y gRPC. Retira UD-05. Adopta las cuatro fases del desastre. Agrega ADR-23 a ADR-29. Separa el ambiente entregado del despliegue objetivo |

## Cómo se cubren las vistas 4+1 y los entregables del enunciado

| Vista o entregable | Sección |
| --- | --- |
| Vista lógica | §5.1 y §5.3 |
| Vista de desarrollo | §5.2 y §5.4 |
| Vista de procesos | §6 |
| Vista física | §7 |
| Escenarios (+1) | §6 y §10 |
| Diagramas de contenedores, componentes y despliegue | Figuras 2 a 5, 11 y 12 |
| Modelo de integración con sistemas externos | §3.2 y §3.3 |
| Diseño de APIs y eventos | §8.4 |
| Decisiones arquitectónicas | §9 |
| Estrategia de seguridad | §8.1 |
| Estrategia de resiliencia y recuperación | §8.2 y §7.2 |
| Estrategia de escalabilidad | §8.3 |

**Convención de estado.** *Construido*: tiene código y pasa la verificación del ambiente entregado (§7.1). *Diseñado* o *sin código*: está decidido, pero no implementado. *En curso*: cambio de código que el equipo está haciendo y que aún no se entrega.

## 1. Introducción y metas

### 1.1 Propósito y alcance

Este documento describe la arquitectura de la Plataforma para la Gestión de Desastres y Reconstrucción y justifica sus decisiones con los atributos de calidad del SRS. Es una sola arquitectura: la que implementa el código entregado, extendida a las partes que aún no tienen código.

| Mínimo del proyecto | Qué se entrega | Dónde |
| --- | --- | --- |
| a. App móvil que recolecta y caracteriza | App de campo .NET MAUI con SQLite: captura sin conexión y sincronización. Caracterizar desde la app está en curso | §6.4, §8.5 |
| b. Reconstrucción física | UD-06 Reconstrucción, en Spring Boot con REST y gRPC | §5.3, §6.5 |
| c. Mapa con desastres, evaluaciones y proyectos | Tablero del desastre en el portal (UD-09), armado por un servlet multihilo | §6.6 |
| d. Evaluación de daños | Evaluaciones versionadas con fotografía en `servidor` (UD-03) | §5.3, §6.4 |
| Autenticación y autorización | Sesión del núcleo validada en cada solicitud; permisos por rol y organismo | §8.1 |

Personas afectadas, gestión de recursos y donaciones quedan fuera de alcance por indicación del profesor (ADR-28): son sistemas externos (SE-04 y SE-05) y UD-05 se retira. Quedan diseñados, sin código: la coordinación de organismos, la consulta pública (UD-07), la integración (UD-08), el modo degradado de `servidor` y el despliegue en tres centros de datos.

### 1.2 Metas de calidad

Cinco escenarios del SRS tienen importancia y dificultad altas, y son los que más condicionan la arquitectura. El resto está en §10.

| Escenario | Qué exige | Consecuencia para el diseño | Estado |
| --- | --- | --- | --- |
| QA-DIS-01 | 99,95 % mensual en las funciones críticas | Redundancia de lo crítico; lo degradable aparte | Meta de diseño |
| QA-DIS-02 | Recuperar lo crítico en 15 min sin perder operaciones confirmadas | Datos en dos centros; conmutación automática | Meta de diseño |
| QA-ESC-01 | De 1.000 a 10.000 usuarios, creciendo solo lo saturado | Unidades separadas por perfil de carga | Unidades construidas; sin prueba de carga |
| QA-ESC-02 | 500 equipos de campo sin afectar la coordinación | Evaluaciones en una unidad distinta del núcleo | Construido; sin prueba de carga |
| QA-CNF-01 | Reconciliar sin sobrescribir tras trabajar sin conexión | Operaciones con identidad propia y conflictos explícitos | Construido y probado |

### 1.3 Metas cuantitativas

| Meta | Valor | Cómo se verifica |
| --- | --- | --- |
| Disponibilidad de las funciones críticas | 99,95 % mensual (21,6 min de caída como máximo) | Meta de diseño del despliegue objetivo |
| Usuarios concurrentes | De 1.000 a 10.000 en 10 minutos | Meta de diseño; modelo de capacidad en §8.3 |
| Consulta frecuente | Menos de 2 s | p50 y p95 medidos en el ambiente entregado (ADR-29) |
| Registro de un desastre | Menos de 3 s | Ídem |
| Alerta crítica | Menos de 5 s, con hasta 10 alertas simultáneas | Ídem; las 10 simultáneas dependen de ADR-27 |
| Recuperación de lo crítico | 15 minutos, 0 operaciones confirmadas perdidas | Meta de diseño |

Una meta de tiempo se cumple cuando el p95 queda dentro del tiempo, ninguna operación supera el doble y menos del 0,1 % termina con error. Cada medición indica el número de solicitudes y la concurrencia.

### 1.4 Interesados

El profesor y los evaluadores esperan decisiones justificadas y evidencia; los coordinadores y autoridades, fases, tablero y proyectos auditados; los evaluadores de campo, una captura que no pierde datos; los operadores, saber desplegar, verificar y recuperar; y los responsables de sistemas externos y el equipo de desarrollo, contratos estables entre unidades.

## 2. Restricciones

### 2.1 Restricciones del SRS

| Restricción | Efecto en el diseño |
| --- | --- |
| CON-01 a CON-03: no se reemplazan los sistemas existentes y cada uno conserva su mecanismo | Un adaptador por sistema externo (ADR-07) |
| CON-04: los sistemas externos pueden no estar disponibles | Tiempo de espera, reintentos, corte de circuito y cola de pendientes |
| CON-05 y CON-06: hay operaciones inmediatas y otras diferibles | Cada interacción declara su mecanismo (§3.3) |
| CON-07: tecnologías de los talleres del curso | Jakarta EE, Spring Boot, .NET, PostgreSQL, RabbitMQ y Nginx, por unidad (ADR-17) |
| CON-08: móvil parcialmente sin conexión | SQLite en el dispositivo y reconciliación (ADR-04, ADR-25) |
| CON-09: protección de datos personales | La plataforma no guarda datos de personas afectadas (ADR-28); las cuentas de usuario se protegen según §8.1 |

### 2.2 Restricciones del curso

| Restricción | Cómo se cumple |
| --- | --- |
| Presentación MVC con un framework | Portal en Spring MVC con Thymeleaf (ADR-23) |
| Multihilo con servlets | `TableroServlet` asíncrono con pool acotado (ADR-24) |
| Componentes .NET para operar sin conexión | App de campo .NET MAUI con SQLite (ADR-25) |
| Autenticación y autorización | Sesión del núcleo validada en cada unidad; permisos en el backend (§8.1) |
| Alcance indicado en clase | Personas, recursos y donaciones como sistemas externos (ADR-28) |

### 2.3 Restricciones organizativas y convenciones

- Se entrega un ambiente de una máquina con Docker Compose (§7.1). Los tres centros de datos quedan diseñados (§7.2).
- Plantilla arc42 y diagramas C4: azul sólido es construido; gris con borde punteado, diseñado.
- Se conservan los identificadores del SRS y de la versión 1.0 (RF, QA, CON, SE, UD, ADR). RN-01 a RN-21 son las reglas de negocio de Reconstrucción.
- Los nombres en monoespaciado (`nucleo`, `servidor`, `desastres`, `campo`, `reconstruccion`, `portal`) son carpetas de código y contenedores.

## 3. Contexto y alcance

### 3.1 Contexto

```mermaid
flowchart LR
  COO["Coordinador"]
  EVA["Evaluador de campo"]
  CNS["Consulta y auditoría"]
  CIU["Ciudadano"]
  P["Plataforma para la Gestión<br/>de Desastres y Reconstrucción"]
  SE1["SE-01 Alertas, meteorología y sismos"]
  SE2["SE-02 GIS"]
  SE3["SE-03 Hospitalarios"]
  SE4["SE-04 Gestión de recursos"]
  SE5["SE-05 Identificación y atención de personas"]
  SE6["SE-06 Administrativos y financieros"]
  SE7["SE-07 Proyectos de infraestructura"]
  SE8["SE-08 Comunicación y notificación"]
  COO -->|portal web| P
  EVA -->|app de campo y portal| P
  CNS -->|portal web| P
  CIU -.->|consulta pública, diseñada| P
  SE1 -.->|alertas| P
  P -->|zonas afectadas| SE2
  P <-.-> SE3
  P <-.-> SE4
  P <-.-> SE5
  P <-.-> SE6
  P <-.-> SE7
  P -.->|avisos y correo| SE8
```

*Figura 1. Contexto de la plataforma. Línea continua: uso o llamada inmediata. Línea punteada: mensajes en cola o canal diseñado.*

| Actor | Rol en la plataforma | Rol en Reconstrucción | Qué hace |
| --- | --- | --- | --- |
| Coordinador | COORDINADOR | AUTORIDAD | Registra el desastre y cambia su fase y nivel; crea, aprueba y cancela proyectos; administra los usuarios de su organismo |
| Evaluador de campo | EVALUADOR | RESPONSABLE | Captura evaluaciones; caracteriza el desastre; opera el proyecto que tiene asignado |
| Consulta | CONSULTA | AUDITOR | Solo lectura, incluida la auditoría |
| Ciudadano | Sin sesión | — | Consulta pública (diseñada) |
| Sistema que no usa REST | Cuenta de la plataforma | — | Inicia sesión por SOAP o por EJB remoto (construido) |

### 3.2 Sistemas externos

Cada sistema externo sigue siendo dueño de sus datos; la plataforma guarda una copia con su fuente y su hora. La columna de protección indica tiempo de espera, reintentos y umbral del corte de circuito.

| Sistema | Mecanismo | Qué cruza la frontera | Protección | Si no responde | Estado |
| --- | --- | --- | --- | --- | --- |
| SE-01a, b, c Alertas, meteorología, sismos | Entrada: cola, o webhook que deja el mensaje en la cola | Fenómeno, magnitud, ubicación y hora; JSON o CAP. Se reconoce por fuente e identificador | El emisor reintenta | Siempre se recibe; lo inválido queda en cuarentena visible | Cola de entrada construida; adaptadores diseñados |
| SE-02 GIS | REST inmediato | Geometrías GeoJSON con la versión de su catálogo | 2 s; 2; 5 fallas en 30 s | Última geometría conocida, marcada «por actualizar» | Diseñado |
| SE-03 Hospitalarios | Cola diferida y consulta cada 60 s | Sale el aviso de desastre; entra la capacidad disponible. Sin datos de pacientes (ADR-28) | 5 s; 3; 3 seguidas | Datos marcados desactualizados; pendientes en cola | Cola del aviso construida; adaptador diseñado |
| SE-04 Gestión de recursos | Cola diferida y consulta cada 5 min | Disponibilidad con su hora; es dueño de recursos y donaciones | 5 s; 3; 3 seguidas | Última disponibilidad con su hora | Diseñado |
| SE-05 Identificación y atención de personas | REST inmediato y cola | Es dueño de los datos de personas; solo cruzarían identificadores | 3 s; 2; 5 en 30 s | Sin efecto en lo crítico | Diseñado |
| SE-06 Administrativos y financieros | Cola diferida | Compromiso presupuestal (proyecto, monto, versión) y su respuesta | 10 s; 5; 3 seguidas | Operación en conciliación (ADR-21) | Diseñado |
| SE-07 Proyectos de infraestructura | Cola diferida | Proyectos, avances, estado y contratista | 10 s; 5; 3 seguidas | Pendientes en cola | Diseñado |
| SE-08 Comunicación y notificación | Salida: cola por sistema suscrito y correo SMTP | Aviso JSON con su identificador | Reintento a los 5 s | Pendiente hasta que el canal confirma; lo no procesable va a errores | Construido |

Un circuito abierto se prueba con una sonda cada 30 s y se cierra con la primera respuesta correcta.

### 3.3 Modelo de integración

Toda comunicación con un sistema externo pasa por su adaptador en la unidad de integración (UD-08, diseñada). Un adaptador tiene cuatro piezas: el **conector**, con el protocolo y la credencial del externo; el **traductor**, entre el formato externo y el modelo común; el **validador**, que rechaza lo inválido con un error explícito; y la **protección**. Ninguna unidad interna conoce formatos ajenos.

| Mecanismo | Cuándo se usa | Garantía |
| --- | --- | --- |
| API REST inmediata | Quien llama necesita la respuesta | Respuesta o error dentro del tiempo de espera |
| Cola diferida | El destino puede no estar; importa que llegue | Al menos una vez; se guarda hasta su aceptación |
| Avisos por cola | Varios interesados en un cambio confirmado | Al menos una vez a cada suscriptor |
| Webhook de entrada | El externo avisa sin que se le pregunte | La recepción queda en cola antes de responder |

Todo mensaje lleva un identificador único y quien lo recibe descarta los repetidos. Cada intercambio tiene un estado visible: Recibido, Validado, Incorporado; en las salidas, Aceptado o En conciliación (ADR-21); o Rechazado, con su causa. Un sistema nuevo es un adaptador nuevo que se despliega solo (QA-EXT-01); uno que solo recibe avisos se suscribe con `POST /sistemas`, sin código nuevo, y eso ya está construido. Un cambio de contrato toca solo el traductor (QA-INT-02).

## 4. Estrategia de solución

### 4.1 Estilo arquitectónico

La plataforma usa servicios de grano grueso, separados por consistencia transaccional y por perfil de carga, cada uno dueño de sus datos. Se llaman por REST o gRPC cuando necesitan la respuesta y se avisan por colas cuando no. Así, lo consistente se guarda en una transacción local y lo que recibe la carga crece por separado.

| Atributo (1 a 5) | A. Monolito modular | B. Núcleo y tres satélites | C. Servicios por consistencia y carga | D. Un servicio por componente |
| --- | --- | --- | --- | --- |
| Disponibilidad de lo crítico | 3 | 3 | 4 | 4 |
| Escalabilidad por partes | 2 | 3 | 4 | 5 |
| Rendimiento | 5 | 4 | 4 | 3 |
| Resiliencia | 2 | 3 | 3 | 5 |
| Interoperabilidad | 3 | 4 | 4 | 4 |
| Modificabilidad | 3 | 3 | 4 | 5 |
| Integridad de los cambios | 5 | 4 | 5 | 3 |
| Simplicidad de operación | 5 | 4 | 3 | 1 |
| **Total** | **28** | **28** | **31** | **30** |

Los puntajes son valoraciones del equipo, no mediciones. A y B pierden en escalabilidad porque el Escenario B pide crecer solo lo necesario. El resultado es sensible: sin la fila de integridad o la de simplicidad ganaría D. Esas filas pesan porque la trazabilidad es obligatoria y es más segura cuando el cambio y su registro son una transacción, y porque la plataforma la opera una entidad pública, no un equipo dedicado a decenas de servicios. Ningún escenario exige lo que D hace mejor: escalar un componente aislado del núcleo.

### 4.2 Patrones

| Patrón | Dónde | Estado |
| --- | --- | --- |
| Transacción local: cambio, historial, auditoría y aviso juntos | Núcleo, `servidor`, Reconstrucción | Construido |
| Bandeja de salida | Núcleo y Reconstrucción | Construido |
| Publicar y suscribir con una cola por suscriptor | Notificaciones | Construido |
| Entrada única | Nginx | Construido |
| MVC con controlador frontal | Portal | Construido |
| Consultas en paralelo con pool acotado y respuesta parcial | `TableroServlet` | Construido |
| Puertos y adaptadores (hexagonal) | Reconstrucción; capas en todas las unidades | Construido |
| Fachada de API | `servidor` ofrece a la app una sola API y delega sesión y desastres en el núcleo | Construido |
| Copia de lectura derivada | Catálogo de desastres en `servidor`; consulta pública | Construido; consulta pública diseñada |
| Adaptador por sistema externo | UD-08 | Diseñado |

### 4.3 Tecnología y relación con Sahana Eden

Java atiende el *back-office* en línea: el núcleo en Jakarta EE, por sus transacciones JTA, y Reconstrucción y el portal en Spring Boot 3.3. .NET atiende lo que trabaja sin conexión: la app MAUI y `servidor`, que comparten los contratos de sincronización (ADR-17).

De Sahana Eden, analizada en el SRS, se toman la plataforma web como punto común, la configurabilidad por catálogos, la información geográfica transversal y el control de acceso por organización. No se toman su unidad única de despliegue ni su base compartida, porque impiden escalar y proteger lo crítico por separado.

## 5. Vista de bloques

### 5.1 Contenedores (C4 nivel 2)

![Contenedores de la plataforma](diagramas/contenedores.png)

*Figura 2. Contenedores de la plataforma (C4 nivel 2). Azul: construido. Gris punteado: diseñado.*

| ID | Unidad | Tecnología | Responsabilidad | Datos propios | Criticidad | Estado |
| --- | --- | --- | --- | --- | --- | --- |
| UD-01 | Entrada (`web`) | Nginx 1.28 | SRV-01: acceso único; enruta y limita tamaño e intentos de inicio de sesión | Ninguno | Crítica | Construida |
| UD-02 | Núcleo (`nucleo`) | Java 21, Jakarta EE, WildFly 41 | SRV-02 identidad y sesiones; SRV-03 desastres (registro, nivel, fases, caracterización); SRV-12 auditoría; bandeja de salida | Base `pgdrdb` | Crítica | Construida; delimitación, consolidación, coordinación y auditoría consolidada diseñadas |
| UD-03 | Campo y mapas (`servidor`) | ASP.NET Core 10 | SRV-05 evaluaciones; SRV-06 fotos; mapa; copia del catálogo de desastres; API de la app | Base `mapas` | Crítica | Construida; validación y duplicados diseñados |
| UD-04 | Notificaciones (`desastres`) | Java 17, Jakarta EE, Payara 6 | SRV-04: cola por suscriptor, correo, registro de entregas; entrada de alertas | Registro de entregas (archivo) | Crítica | Construida; consumo continuo en curso |
| UD-05 | Personas afectadas | — | SRV-07 | — | — | Retirada (ADR-28) |
| UD-06 | Reconstrucción (`reconstruccion`) | Java 17, Spring Boot 3.3, REST y gRPC | SRV-09: proyectos de reconstrucción física; RN-01 a RN-21 | Base `reconstruccion` | Degradable | Construida |
| UD-07 | Consulta pública | ASP.NET Core | SRV-10: copia pública de solo lectura | Copia derivada | Degradable | Diseñada |
| UD-08 | Integración | Jakarta EE, RabbitMQ | SRV-11: un adaptador por sistema externo | Pendientes | Según adaptador | Diseñada |
| UD-09 | Portal web (`portal`) | Java 21, Spring Boot 3.3, Spring MVC, Thymeleaf | Única interfaz web; `TableroServlet` | Sesión HTTP en memoria | Crítica | Construida |
| — | App de campo (`campo`) | .NET MAUI, SQLite | Captura sin conexión y sincronización | Capturas por organismo y usuario | Crítica | Construida; mejoras en curso |
| INF-01 | Cola de mensajes | RabbitMQ 4.1 | — | Mensajes en tránsito | Crítica | Construida, un nodo |
| INF-02 | Monitoreo | Sondas y métricas | — | Métricas | Crítica | Solo sondas de salud |

SRV-07 (personas) y SRV-08 (recursos) salen de alcance (ADR-28). El código de Solicitudes de recursos del Taller 2 sigue en el núcleo y en `servidor` hasta que el equipo acuerde retirarlo; el portal ya no lo ofrece y este documento no lo trata como funcionalidad. La web estática de `servidor` y la interfaz React de Reconstrucción ya no se sirven.

**Agrupación.** Identidad, desastres y auditoría van juntos en el núcleo porque comparten transacciones. La evaluación de daños va aparte porque recibe la escritura de 500 equipos (QA-ESC-02), y la foto va con su evaluación (ADR-13). Las notificaciones van aparte para que el registro siga aunque fallen RabbitMQ o el correo. Reconstrucción va aparte porque opera meses después y es degradable. El portal va aparte porque no guarda estado de vista y habla con cada dueño de datos. Ninguna unidad lee la base de otra.

### 5.2 Componentes (C4 nivel 3)

#### 5.2.1 Núcleo (UD-02)

![Componentes del núcleo](diagramas/componentes_nucleo.png)

*Figura 3. Componentes del núcleo (C4 nivel 3).*

`TokenAuthFilter` valida el token en cada solicitud y compara la clave de integración en tiempo constante. `AuthService` maneja usuarios, contraseñas y tokens, y lo usan por igual REST, SOAP y EJB remoto. `RegistroIntegrado` registra el desastre y cambia nivel, fase y caracterización con su historial, en una transacción JTA. `PublicadorOutbox` y `BandejaOutbox` entregan los avisos a notificaciones y purgan las sesiones vencidas (§8.6).

#### 5.2.2 Portal web (UD-09)

![Componentes del portal web](diagramas/componentes_portal.png)

*Figura 4. Componentes del portal web (C4 nivel 3): MVC y servlet multihilo.*

| Parte del MVC | En el portal |
| --- | --- |
| Controlador | `DispatcherServlet` de Spring como controlador frontal; clases `@Controller` de sesión, desastres, evaluaciones, mapa, Reconstrucción y usuarios |
| Modelo | DTO y modelos de vista que arman `NucleoCliente` (REST), `ServidorCliente` (REST) y `ReconstruccionCliente` (gRPC) |
| Vista | Plantillas Thymeleaf; el mapa usa Leaflet, servido por el portal |

Pantallas: inicio de sesión; desastres con filtro por fase, registro, caracterización, cambio de fase e historial; tablero con mapa; evaluaciones con historial y foto; Reconstrucción completa; usuarios del organismo. `InterceptorSesion` envía al inicio de sesión a quien no tiene sesión. Los botones dependen del rol, pero cada servicio valida los permisos.

#### 5.2.3 Reconstrucción (UD-06)

![Componentes de Reconstrucción](diagramas/componentes_reconstruccion.png)

*Figura 5. Componentes de Reconstrucción (C4 nivel 3): arquitectura hexagonal.*

- **Entrada:** REST (`ReconstruccionController`) y gRPC (`ReconstruccionGrpcService`, 14 RPC). Ambos validan el token contra el núcleo y aceptan la versión esperada: `If-Match` en REST y `version_esperada` en gRPC.
- **Casos de uso CU-01 a CU-09:** autorizan por rol y organismo, auditan cambios e intentos rechazados (RN-17) y abren la transacción.
- **Dominio:** `Proyecto`, `EstadoProyecto`, `EstadoDesastre` y `EvaluacionDano`, sin Spring, gRPC ni JDBC; 31 pruebas en memoria.
- **Salida:** repositorio con control optimista; bandeja de salida hacia RabbitMQ; `DesastresNucleo` (catálogo y cambio de fase); `EvaluacionesServidor` (evaluaciones, con el token del usuario).

En `servidor` (UD-03) los componentes son la fachada de API, la recepción de lotes inmediata o diferida (ADR-18), las evaluaciones versionadas, los mapas y la copia del catálogo de desastres.

### 5.3 Reglas de dominio por capacidad

#### Gestión de desastres (núcleo)

El registro guarda identificador del cliente, tipo (lista fija), fecha, ubicación, nivel de 1 a 4, descripción, organismo (de la sesión), fase y versión; la caracterización agrega punto y radio. Quedan diseñados el área delimitada con versiones, la fuente de la información y el responsable de la emergencia.

**Fases.** Son las cuatro del profesor (Registro, Caracterización, Reconstrucción y Cierre), en una columna con CHECK.

| Transición | Quién la pide | Condición |
| --- | --- | --- |
| Registro → Caracterización | El coordinador, o automática con la primera caracterización | — |
| Caracterización → Reconstrucción | Solo el módulo de Reconstrucción (token del usuario y clave de integración) | RN-01, RN-02: al menos una evaluación validada con daño moderado o superior |
| Reconstrucción → Cierre | Solo el módulo de Reconstrucción | RN-04: ningún proyecto sin finalizar ni cancelar |
| Registro o Caracterización → Cierre | El coordinador, no Reconstrucción (RN-03) | Motivo, por ejemplo una falsa alarma |
| Desde Cierre | Nadie | Terminal (RN-05) |

Todo cambio de fase exige motivo y guarda versión, valor anterior y nuevo, y autor. Si un coordinador pide por la API una transición que depende de RN-01, RN-02 o RN-04, el núcleo la rechaza. *Equivalencia con los estados de ejemplo del enunciado:* Detectado y Registrado → Registro; Activo, En evaluación y En atención → Caracterización; En recuperación y En reconstrucción → Reconstrucción; Cerrado → Cierre.

**Caracterización (contrato C2).** `PATCH /eventos/{id}/caracterizacion` recibe latitud, longitud, radio en km (hasta 500), nivel de 1 a 4 opcional, descripción, versión base y `claveIdempotencia`. Repetir la clave devuelve lo mismo; una versión vieja responde 409 `VERSION_DESACTUALIZADA` con la versión actual. Queda en el historial y, si cambia el nivel, emite un aviso. La hacen coordinador y evaluador. El nivel 1 a 4 del desastre no es el nivel de afectación 1 a 5 de una evaluación.

#### Evaluación de daños (`servidor` y app)

| Aspecto | Construido | Diseñado o en curso |
| --- | --- | --- |
| Datos | Identificador del dispositivo, desastre, evaluador y organismo de la sesión, ubicación, activo en texto, nivel de afectación 1 a 5, descripción, foto y versión | Tipo de activo con catálogo (C1), en curso |
| Estados | Preliminar (sin desastre) y Registrada | Validada por un coordinador distinto del evaluador, Devuelta y Duplicada |
| Evidencias | Una foto JPEG o PNG de hasta 5 MB; correcciones con historial que no tocan la captura original | Documentos PDF; hasta cinco evidencias |
| Uso | Mapa de calor y panorama | Estimación de costo y prioridad por zona |

Mientras no exista la validación, Reconstrucción considera validada una evaluación vinculada a un desastre registrado (§11).

#### Reconstrucción física (UD-06)

| Transición del proyecto | Quién | Condición |
| --- | --- | --- |
| Propuesto → Aprobado | Una autoridad distinta de quien lo formuló (RN-20) | Presupuesto mayor que cero, financiación y responsable (RN-11, RN-12) |
| Aprobado → En ejecución | El responsable asignado (RN-21) | Contratista y cronograma (RN-11) |
| En ejecución ↔ Suspendido | Autoridad o responsable asignado | Motivo (RN-16) |
| En ejecución → Finalizado | Autoridad o responsable asignado | Avance 100 %, una evidencia como mínimo y sin sobrecosto (RN-15) |
| No terminal → Cancelado | Solo la autoridad (RN-21) | Motivo; sus evaluaciones quedan libres |

Finalizado y Cancelado son terminales (RN-16). Un proyecto nace con el desastre en Reconstrucción (RN-06), de evaluaciones del mismo desastre y organismo, sin repetirlas (RN-07); con destrucción total es de reconstrucción y no de reparación (RN-08). Una evaluación origina a lo sumo un proyecto no cancelado, y lo garantiza la base con la tabla `evaluaciones_tomadas` (RN-09). En lote, cada ítem va en su transacción (RN-18). Cada organismo ve solo sus proyectos (RN-10). Solo la autoridad modifica el presupuesto, con motivo (RN-12); el avance solo baja como corrección con motivo (RN-13); el sobrecosto se marca y se avisa sin bloquear el registro del costo (RN-14). El tipo de activo se copia de las evaluaciones y el avance del desastre se pondera por presupuesto. No se guardan datos de personas. El presupuesto es más simple que en la versión 1.0, sin versiones propuestas y autorizadas por separado; el compromiso ante SE-06 queda diseñado (ADR-21).

#### Diseñados sin código

La coordinación de organismos (participación por desastre y tareas con estados Pendiente, Asignada, En curso, Terminada y Cancelada); los catálogos configurables (ADR-08), hoy listas fijas; la consulta pública, solo con lo publicable; la delimitación con dos fechas (ADR-06) y la consolidación con alias (ADR-05).

### 5.4 Organización del código

| Capa | Contiene | Depende de |
| --- | --- | --- |
| Dominio | Entidades y reglas | Nada |
| Casos de uso | Una operación por intención de negocio | Dominio |
| Adaptadores | Controladores REST y gRPC, mensajería, repositorios | Casos de uso |
| Infraestructura | Base de datos, colas, red, configuración | Adaptadores |

Hay una carpeta y un contenedor por unidad; `interop` guarda los clientes de prueba de SOAP y EJB remoto. Ninguna unidad importa código de otra: se comunican por contratos REST, gRPC (`reconstruccion.proto`, copiado en el portal) y de eventos (§8.4). Las pruebas de punta a punta están en `servidor/scripts` y `portal/scripts`, y `./verificar.sh` las corre todas.

## 6. Vista de ejecución

Esta sección recorre los escenarios A a E del enunciado, el tablero multihilo y la sincronización sin conexión. Flecha continua: llamada inmediata. Flecha punteada: mensaje en cola o respuesta diferida.

### 6.1 Escenario A — Inicio de una emergencia

```mermaid
sequenceDiagram
  participant S as SE-01 Sensor sísmico
  participant N as UD-04 Notificaciones
  participant G as UD-02 Núcleo
  participant Q as RabbitMQ
  participant O as Organismos y sistemas suscritos
  participant X as SE-02 GIS vía UD-08
  participant C as Coordinador o evaluador
  S-->>N: Detección por el adaptador y la cola de entrada
  N->>N: Valida y descarta repetidos
  N->>G: Registra el evento preliminar
  G->>G: Guarda el desastre en Registro y su aviso en una transacción
  G->>N: Entrega el aviso desde la bandeja de salida
  N-->>Q: Publica en la cola de cada suscriptor
  Q-->>O: Aviso
  N->>O: Correo
  G->>X: Pide las zonas afectadas
  X->>G: Zonas
  C->>G: Caracteriza con ubicación, radio y nivel
  G->>G: Pasa a Caracterización, que equivale a Activo
```

*Figura 6. Escenario A: inicio de una emergencia.*

**Construido:** registro con su aviso en una transacción, bandeja de salida, cola por suscriptor, correo, caracterización y cambio de fase. **Diseñado:** los adaptadores de SE-01 y SE-02 y el registro automático del evento preliminar; la cola de entrada ya existe. La alerta no espera al GIS y se cuenta hasta que el aviso queda en la cola de cada suscriptor y el servidor de correo lo acepta.

| Tramo de la alerta | Presupuesto |
| --- | --- |
| Guardar el evento y su aviso, con réplica en otro centro | 0,8 s |
| Espera en la bandeja de salida (ciclo de 1 s) | 1,0 s |
| Entrega a notificaciones y publicación | 0,4 s |
| Consumo, cola de cada suscriptor y correo | 1,5 s |
| Margen | 1,3 s |
| **Total (QA-REN-03)** | **5,0 s** |

Con avisos aislados, la versión 1.0 midió 2,7 s y 3,2 s, con un ciclo de 5 s que hoy es de 1 s. Con 10 alertas simultáneas aún no se cumple, porque el consumidor toma un mensaje por segundo de cada cola (ADR-27, en curso).

| Situación | Qué hace la plataforma |
| --- | --- |
| Mensaje inválido o repetido | Lo inválido va a la cola de errores; lo repetido se descarta por fuente e identificador |
| El núcleo no responde al registrar | El mensaje vuelve a su cola de reintento cada 5 s |
| El GIS no responde | El evento se crea; la geometría queda pendiente |
| Notificaciones o RabbitMQ no responden | El aviso espera en la bandeja de salida (verificado deteniendo RabbitMQ) |

### 6.2 Escenario B — Aumento repentino de usuarios

Los ciudadanos usarían la consulta pública (diseñada), que lee su propia copia desde caché, con límite de tasa, y nunca llama al núcleo. Los funcionarios usan el portal y la app. Cada unidad crece por separado cuando su métrica lo pide (§8.3). El tablero acota sus hilos: con el pool lleno responde 503 en lugar de cargar más a los servicios de atrás.

| Situación | Qué hace la plataforma |
| --- | --- |
| Las consultas ciudadanas superan el límite | La entrada responde 429; los funcionarios usan otras rutas |
| Una unidad pasa de 60 % de uso sostenido | Se agregan instancias solo de esa unidad |
| La carga supera la capacidad | Se degrada en el orden de §8.2; lo crítico no |
| El núcleo no responde | Las operaciones protegidas se rechazan; la consulta pública sigue |

Evidencia: el tablero con 20 clientes responde con p95 de 241 ms en una máquina (§6.6). No hay prueba con 10.000 usuarios.

### 6.3 Escenario C — Falla de un sistema externo

```mermaid
sequenceDiagram
  participant G as UD-02 Núcleo
  participant N as UD-04 Notificaciones
  participant Q as RabbitMQ
  participant I as UD-08 Adaptador hospitalario, diseñado
  participant H as SE-03 Sistema hospitalario
  G->>N: Aviso de desastre desde la bandeja de salida
  N-->>Q: Cola propia del sistema hospitalario
  Q-->>I: Aviso
  I->>H: Envío con tiempo de espera de 5 s
  H--xI: Sin respuesta
  I->>H: Reintentos 1, 2 y 3
  I->>I: Abre el circuito y marca el sistema como no disponible
  Note over Q,I: Los avisos siguientes esperan en la cola sin afectar a otros suscriptores
  loop Cada 30 s
    I->>H: Sonda
  end
  H->>I: Responde
  I->>H: Envía los pendientes en orden
  I->>H: Pide la capacidad desde la última sincronización
  H->>I: Datos
```

*Figura 7. Escenario C: falla del sistema hospitalario.*

Mientras el hospital no responde, las demás unidades y suscriptores siguen trabajando, porque ninguno lo llama. **Construido:** cada sistema suscrito tiene su cola durable, y si deja de consumir, sus avisos esperan sin afectar a los demás. **Diseñado:** el adaptador con corte de circuito y la consulta de capacidad. Tras una caída de horas, los pendientes salen por lotes y lo más reciente primero; los datos viejos se muestran con su hora; una operación sin respuesta en el plazo queda En conciliación (ADR-21).

### 6.4 Escenario D — Evaluación distribuida y sincronización sin conexión

```mermaid
sequenceDiagram
  participant D as App de campo
  participant GW as UD-01 Entrada
  participant E as UD-03 servidor
  participant G as UD-02 Núcleo
  D->>D: Guarda evaluaciones y fotos sin conexión en SQLite
  D->>GW: Lote de hasta 20 evaluaciones sin foto
  GW->>E: POST /api/campo/lotes
  E->>G: Valida la sesión
  G->>E: Usuario, rol y organismo
  E->>E: Compara cada operación con la versión vigente
  E->>D: Por operación, incorporada, repetida, conflicto o rechazada
  D->>GW: Una evaluación con su foto por petición
  GW->>E: Evaluación y foto
  E->>D: Recibida
  D->>D: Marca como sincronizada
```

*Figura 8. Escenario D: sincronización de evaluaciones de campo.*

Las fotos van después de los lotes, una por petición, para que una foto pesada no retrase a las demás. Evaluador y organismo salen de la sesión, no de lo que envía la app. El tiempo de confirmación no está medido en campo; se estima en unos 8 s para subir una foto de 1 MB a 1 Mbps, más 1,8 s para confirmar, frente a la meta de 10 minutos (QA-ESC-02). Hoy la app sincroniza cada 8 s mientras la pantalla de captura está abierta; lo que está en curso se describe en §8.5.

| Situación | Qué hace la plataforma |
| --- | --- |
| Se corta una evaluación con foto, o se reenvía un lote | No queda nada a medias; lo repetido responde «repetida» y no se duplica |
| Otra persona corrigió antes la evaluación | Conflicto: se conservan la propuesta y el dato del servidor, y decide el evaluador |
| La sesión venció | 401; la app debe conservar los pendientes y pedir sesión (en curso) |
| El núcleo no responde | `servidor` rechaza; la app sigue capturando y reintenta |
| El desastre aún no está en la copia de `servidor` | La evaluación queda preliminar y se vincula después |
| La carga supera la capacidad | Incorporación diferida (ADR-18) |

### 6.5 Escenario E — Reconstrucción

```mermaid
sequenceDiagram
  participant A as Autoridad en el portal
  participant P as UD-09 Portal
  participant R as UD-06 Reconstrucción
  participant G as UD-02 Núcleo
  participant E as UD-03 servidor
  participant Q as RabbitMQ
  A->>P: Iniciar reconstrucción
  P->>R: IniciarReconstruccion por gRPC con el token
  R->>G: Valida la sesión
  R->>E: Lee las evaluaciones del desastre
  R->>G: Pide la fase Reconstrucción con la clave de integración
  A->>P: Crea proyectos desde las evaluaciones
  P->>R: CrearProyectosEnLote
  R->>R: Cada proyecto en su transacción, con auditoría y aviso
  A->>P: Otra autoridad aprueba con presupuesto y responsable
  P->>R: AprobarProyecto con version_esperada
  R-->>Q: PROYECTO_APROBADO desde la bandeja de salida
  A->>P: Consulta seguimiento y auditoría
  P->>R: ConsultarSeguimiento y ConsultarAuditoria
```

*Figura 9. Escenario E: reconstrucción física.*

Cada proyecto guarda una copia de sus evaluaciones de origen y la conserva aunque la evaluación se corrija. Las reglas están en §5.3. Lo prueban `verificar_reconstruccion.py` (76 a 78 comprobaciones, incluidas aprobación por la misma persona, sobrecosto, cancelación sin permiso y gRPC) y 31 pruebas del dominio. Si `servidor` no responde, no se crea el proyecto y se reintenta con el mismo identificador; si dos personas cambian el proyecto a la vez, la segunda recibe 409; si RabbitMQ no responde, el aviso espera en la bandeja de salida; un compromiso sin respuesta del sistema financiero quedaría En conciliación (ADR-21, diseñado).

### 6.6 Tablero de un desastre (servlet multihilo)

![Secuencia del tablero](diagramas/secuencia_tablero.png)

*Figura 10. Tablero de un desastre: servlet asíncrono con tres fuentes en paralelo.*

1. `TableroServlet` atiende `GET /tablero`, valida la sesión y llama a `startAsync`, que libera el hilo de Tomcat.
2. Un pool de 16 hilos con cola de 64 consulta en paralelo el desastre (núcleo), sus evaluaciones (`servidor`) y sus proyectos (Reconstrucción, por gRPC). Cada fuente tiene 1,5 s de límite, y la respuesta, 3 s.
3. Si una fuente falla o vence, responde con las demás e indica `fuentesNoDisponibles`.
4. Con el pool y la cola llenos responde 503 con `Retry-After`; nunca encola sin límite.

Medición del 15-nov-2026 en el ambiente entregado (`evidencias/rol1-tablero/medicion_tablero_2026-11-15_2329.json`):

| Escenario | Solicitudes | Clientes | p50 | p95 | Lectura |
| --- | --- | --- | --- | --- | --- |
| Paralelo | 100 | 1 | 27 ms | 44 ms | Un 27 % más rápido en p95: tarda lo de la fuente más lenta |
| Secuencial | 100 | 1 | 37 ms | 61 ms | |
| Paralelo | 300 | 20 | 173 ms | 241 ms | Igual al secuencial (3 % más) en una máquina saturada |
| Secuencial | 300 | 20 | 153 ms | 233 ms | |
| Reconstrucción congelada | 60 | 20 | 1.510 ms | 1.523 ms | 60 de 60 respuestas parciales, sin errores |

Con 20 clientes, el paralelo triplica la carga simultánea sobre servicios ya saturados, que validan todos el token contra el núcleo: con una sola máquina no gana, y es un trade-off aceptado (ADR-24). Medir corrigió dos defectos:

- Con el plazo de 5 s de las páginas, una fuente colgada retenía los hilos y 25 de 60 respuestas eran 503. Hoy el plazo de cada cliente del tablero es el límite de su fuente.
- La fuente de Reconstrucción descargaba el catálogo completo del núcleo en cada consulta. Hoy las consultas de solo lectura usan una copia de hasta 3 s; las operaciones que deciden reglas leen siempre el dato fresco.

La medición anterior a estos ajustes (`medicion_tablero_2026-11-15_1334.json`: p95 de 56 ms frente a 67 ms con 1 cliente y de 323 ms frente a 288 ms con 20) se conserva como evidencia del cambio.

## 7. Vista de despliegue

### 7.1 Ambiente entregado: Docker Compose en una máquina

![Despliegue entregado](diagramas/despliegue_entregado.png)

*Figura 11. Despliegue entregado: Docker Compose en una máquina, más el teléfono Android.*

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
| Teléfono Android | App de campo | .NET MAUI + SQLite | — | `web` → `api` (`/api/campo/lotes`) |

- **Inicio y secretos.** `./iniciar.sh` construye las imágenes, aplica las migraciones del núcleo y levanta los 11 contenedores. Los secretos viven en `servidor/.env`, que no se versiona y que `iniciar.sh` crea desde `.env.ejemplo`; Compose no tiene contraseñas escritas. Los puertos solo escuchan en 127.0.0.1.
- **Verificación.** `./verificar.sh` corre 9 pruebas de punta a punta: API, integración, campo, Reconstrucción con gRPC, evolución, unificación, caracterización, portal y seguridad. El 15-nov-2026 pasaron con 247 comprobaciones; el número varía en dos o tres entre corridas porque algunas se repiten dentro de un ciclo de espera. La instalación en limpio, desde un clon nuevo, también pasa (`evidencias/rol1-build/`).
- **Alcance.** Este ambiente prueba el comportamiento funcional y sirve para medir (ADR-29). No prueba la disponibilidad ni la carga de 10.000 usuarios.

### 7.2 Despliegue objetivo: tres centros de datos (diseñado)

![Despliegue objetivo](diagramas/despliegue_objetivo.png)

*Figura 12. Despliegue objetivo en tres centros de datos (diseñado, sin montar).*

Las unidades no guardan estado y corren en los tres centros. Cada base PostgreSQL tiene un primario y dos réplicas síncronas, y un cambio se confirma cuando está en el primario y en una réplica. RabbitMQ replica sus colas en tres nodos. Cada 15 minutos se toma una copia aislada con versiones. El centro C solo aloja lo crítico, para mantener la mayoría de dos sobre tres. La sesión vive en la base del núcleo y se valida contra el primario vigente, para que una revocación surta efecto de inmediato.

| Elemento | Centros | Instancias mínimas | Crecimiento |
| --- | --- | --- | --- |
| UD-01 Entrada, UD-02 Núcleo, UD-03 Campo y mapas, UD-09 Portal | A, B, C | 2 por centro | Automático por uso o por tasa |
| UD-04 Notificaciones | A, B, C | 1 por centro | Fijo; requiere ADR-22 |
| UD-06 Reconstrucción y UD-08 Integración | A, B | 1 por centro | Fijo |
| UD-07 Consulta pública | A, B | 2 por centro | Automático, hasta 10 veces |
| INF-01 RabbitMQ e INF-02 Monitoreo | A, B, C | 1 por centro | Fijo |

**Cálculo ilustrativo de disponibilidad.** Muestra por qué tres centros; no demuestra el 99,95 %. Si cada centro está disponible el 99,5 % (p = 0,995) y sus fallas son independientes, al menos dos de tres lo están con probabilidad 3p²(1 − p) + p³ = 0,999925: unos 3 minutos de caída al mes. Quedan fuera los errores de software y de despliegue, la red entre centros, la conmutación (hasta 5 minutos) y los errores de operación, que deben caber en los 18 minutos restantes; solo la operación real puede confirmarlo. Los centros no deben compartir proveedor de energía ni de red.

## 8. Conceptos transversales

### 8.1 Seguridad

Tres reglas: se niega lo que no está permitido; cada permiso depende del rol y del organismo; toda operación crítica deja registro.

| Aspecto del enunciado | Construido | Diseñado o pendiente |
| --- | --- | --- |
| Autenticación | El núcleo es la autoridad de usuarios, organismos y roles; contraseñas con PBKDF2; el inicio de sesión tarda lo mismo exista o no el usuario; Nginx limita a 30 intentos por minuto por IP (429). SOAP y EJB remoto usan las mismas cuentas | Credencial por sistema externo |
| Gestión de sesiones | Token de 8 horas (un turno); el núcleo guarda solo su hash y cada unidad lo valida en cada solicitud. Cerrar sesión lo revoca en REST, SOAP y EJB remoto. Las sesiones vencidas se purgan cada 15 min. En el portal, el token vive en la sesión del servidor y el navegador solo tiene una cookie HttpOnly, sin id en la URL | Cerrar sesiones al cambiar rol u organismo; modo degradado de `servidor` |
| Autorización y roles | Tres roles por organismo, tomados de la sesión y nunca del cliente. Cada servicio valida en el backend; el portal solo oculta botones. Reconstrucción aplica RN-10, RN-20 y RN-21 | Ámbito de evento (ADR-19) |
| Control por organización | Núcleo, `servidor` y Reconstrucción filtran toda lectura y escritura por el organismo de la sesión; las fotos tienen los permisos de su evaluación | Información compartida en un mismo evento |
| Cifrado | Token de la app en el almacenamiento seguro del dispositivo | TLS; cifrado en reposo; llaves fuera de las unidades. El SQLite del móvil aún no se cifra |
| Auditoría | Núcleo: creación del desastre e historial de nivel, fase y caracterización, en la transacción del cambio. Reconstrucción: cambios e intentos rechazados (RN-17) | Registro de solo adición; consulta consolidada (ADR-20); accesos denegados e inicios de sesión |
| Datos personales | No se guardan datos de personas afectadas (ADR-28) | Con SE-03 y SE-05 solo cruzarían identificadores |
| Secretos | En `servidor/.env`, sin versionar. Clave de integración comparada en tiempo constante. `/integracion/importar` apagada por defecto | Autenticación en el servicio de notificaciones; clave EJB fuera de la construcción del núcleo |

**Dos personas.** Un coordinador puede crear otro coordinador de su organismo; así existe la segunda persona que RN-20 exige para aprobar. Con la misma regla quedan diseñadas las operaciones de alto impacto: cambiar políticas de acceso o de sesión, certificar una regla de consolidación, declarar una falla grave y restaurar la copia aislada.

**Modo degradado de `servidor` (diseñado).** Hoy, sin el núcleo, `servidor` rechaza todo. El diseño: solo mientras el núcleo no responde, permite las consultas del propio organismo con la última validación exitosa de la sesión, durante 15 minutos desde ella, sin renovarse ni pasar del vencimiento. Recibe las capturas como pendientes, sin mostrarlas, y responde 503 a lo demás. Rechaza una sesión que no había validado, y olvida la de una sesión cerrada o declarada inválida. Al volver el núcleo, incorpora cada captura solo si su sesión seguía vigente cuando llegó; si no, la conserva rechazada con su motivo.

### 8.2 Resiliencia y recuperación

| Mecanismo | Dónde y con qué valores | Estado |
| --- | --- | --- |
| Tiempos de espera | Entrada a las unidades: 5 s de conexión. Portal: 2 s de conexión y 5 s de lectura; 1,5 s por fuente en el tablero. `servidor` al núcleo: 5 s. Núcleo a notificaciones: 2 s y 3 s. Adaptadores: 2 a 10 s | Construido, salvo adaptadores |
| Reintentos | Bandeja de salida en cada ciclo; cola de reintento a los 5 s; la app reenvía con los mismos identificadores | Construido |
| Corte de circuito | Por adaptador: 3 fallas seguidas o 5 en 30 s; sonda cada 30 s | Diseñado |
| Colas | Entrada de alertas, una por suscriptor, correo, eventos de Reconstrucción; durables | Construido en un nodo |
| Procesamiento asíncrono | Notificaciones, lotes diferidos y servlet del tablero | Construido |
| Degradación controlada | Respuesta parcial del tablero y 503 con el pool lleno; orden de abajo | Tablero construido; resto diseñado |
| Recuperación automática | Nueva instancia en 2 min; conmutación entre centros en 5 min | Diseñado |

Cada unidad corre en su proceso con su grupo de conexiones, y el pool del tablero no comparte hilos con Tomcat. Dos evidencias están medidas: la bandeja de salida no pierde avisos al detener RabbitMQ, y el tablero responde parcial con Reconstrucción caída.

**Orden de degradación.** 1) Consultas históricas y estimaciones: 503 con hora de reintento. 2) Fotos: primero las evaluaciones sin foto. 3) Consulta pública: última copia en caché; luego, límite de tasa. 4) Reconstrucción: solo lectura; el tablero sigue sin proyectos. 5) Integraciones diferidas: se acumulan en cola. 6) Lo crítico nunca se degrada por la carga de lo anterior.

En el despliegue objetivo, cada tipo de falla se trata así:

| Falla | Respuesta | Automática | Restablecimiento | Pérdida |
| --- | --- | --- | --- | --- |
| Cae una instancia (sonda cada 5 s) | El tráfico va a las demás y se crea otra | Sí | Sin interrupción | Ninguna |
| Caen las instancias del núcleo de un centro | Tráfico a otro centro; `servidor` en modo degradado; la app sigue capturando | Sí | 5 min | Ninguna |
| Falla un sistema externo | Contención en el adaptador; pendientes en cola | Sí | Sin interrupción de lo crítico | Ninguna |
| Se pierde un centro | Los otros dos siguen confirmando | Sí | 5 min | Ninguna: cada cambio estaba en 2 de 3 |
| Se pierden dos centros | Dos personas declaran la falla, bloquean los centros perdidos y el sobreviviente asume | No | 15 min | Posible: lo confirmado solo en los perdidos |
| Corrupción lógica | Se detienen las escrituras y se restaura la copia aislada | No: dos personas | 4 horas | Hasta 15 min; se completa con los dispositivos |

QA-DIS-02 se cumple con la cuarta fila; las dos últimas son más raras y el SRS no exige cubrirlas sin pérdida.

**Conmutación (5 minutos).** Los dos centros restantes confirman la pérdida (30 s), el balanceador retira el tráfico (30 s), una réplica asume si allí estaba el primario (1 min), las unidades críticas crecen (2 min) y queda 1 minuto de margen. Un coordinador con un miembro por centro mantiene el turno del primario con mayoría de dos y promueve la réplica más adelantada; el primario anterior queda bloqueado, para que nunca haya dos. Las solicitudes en curso se reintentan con el mismo identificador. Todo es diseño, salvo que varias instancias no publiquen el mismo aviso (§8.6).

**Tras recuperar.** Lo crítico vuelve en 15 minutos, lo esencial en 1 hora y lo degradable en 4 horas. Los avisos pendientes salen en el siguiente ciclo y los dispositivos sincronizan lo que guardaban; un cambio sobre una versión perdida entra como conflicto. Estos tiempos son metas de diseño, que se comprobarían con un simulacro trimestral.

### 8.3 Escalabilidad y rendimiento

Cada unidad crece por separado, agregando instancias iguales, al superar 60 % de uso sostenido durante 2 minutos (diseñado). El núcleo y `servidor` se dimensionan para quedar bajo 50 % en el pico y bajo 70 % con un centro perdido.

| Fuente de carga | Supuesto | Carga resultante | Unidad |
| --- | --- | --- | --- |
| 8.000 ciudadanos | Una consulta cada 30 s | 267 consultas por segundo | UD-07, casi todo desde caché |
| 2.000 funcionarios | Una operación cada 10 s; 80 % consultas | 160 consultas y 40 escrituras por segundo | Portal, `servidor` y núcleo |
| 500 equipos de campo | Un reporte por minuto con foto de 1 MB | 8 registros y 8 MB por segundo; ráfagas al reconectar | UD-03 |
| Validación de sesiones | Una por solicitud protegida | Cerca de 210 lecturas por segundo | UD-02 |
| Sistemas externos | Consultas periódicas y alertas | Menos de 10 mensajes por segundo | UD-08 y UD-04 |

**Modelo de capacidad (estimación).** Supone 100 operaciones por segundo por instancia del núcleo o de `servidor`, y 500 respuestas desde caché por instancia de consulta pública; una prueba de carga debe reemplazar ese supuesto. Como referencia, una instancia de `servidor` atendió entre 314 y 350 consultas por segundo en una comprobación local de la versión 1.0.

| Unidad | Pico | Instancias mínimas | Uso en el pico | Con un centro menos |
| --- | --- | --- | --- | --- |
| UD-03 Campo y mapas | 210 operaciones por segundo | 6 | 35 % | 53 % |
| UD-02 Núcleo | 273 operaciones por segundo | 6 | 46 % | 68 % |
| UD-07 Consulta pública | 267 consultas por segundo | 4 | 13 % | 27 % |

Con 80.000 ciudadanos solo crecería la consulta pública, de 4 a 9 instancias. Con fotos de 5 MB, `servidor` pasaría de 6 a 12. Si la capacidad real fuera la mitad, el núcleo quedaría al 91 % y habría que duplicarlo.

| Tramo | Consulta (QA-REN-01, 2 s) | Registro (QA-REN-02, 3 s) |
| --- | --- | --- |
| Red hasta la entrada | 0,5 s | 0,5 s |
| Entrada y enrutamiento | 0,1 s | 0,1 s |
| Validación de la sesión | 0,2 s | 0,2 s |
| Unidad dueña: lectura, o guardado con aviso y réplica | 0,5 s | 0,9 s |
| Respuesta | 0,4 s | 0,4 s |
| Margen | 0,3 s | 0,9 s |

**Medido en el ambiente entregado.** El tablero, la consulta más compuesta, tuvo p95 de 44 ms con un cliente y de 241 ms con 20 (§6.6); en la versión 1.0, el registro de un desastre respondió en 22 ms con poca carga. Ninguna cifra incluye red real, réplica ni 10.000 usuarios. Confirmar en otro centro consumiría cerca de 0,8 s de los 3 s del registro, y con fotos de 5 MB el volumen de campo se multiplica por cinco. Las listas aún no se paginan (diseñado: de 20 en 20 con cursor).

### 8.4 APIs y eventos

| Convención | Regla |
| --- | --- |
| Versión | Prefijo `/api` sin número; un cambio incompatible se publica en `/api/v2` y convive con el anterior |
| Autenticación | `Authorization: Bearer` con el token; en gRPC, el mismo token en el metadato `authorization` |
| Repetición segura | Toda escritura lleva un identificador del cliente; repetirla no duplica |
| Control de versión | `versionBase` en el núcleo y en `servidor`; `If-Match` o `version_esperada` en Reconstrucción. Si cambió, 409 |
| Formato | JSON; fechas ISO 8601 con zona; geometrías GeoJSON |

Códigos HTTP: 200 o 201 aplicada; 202 recibida para proceso diferido; 401 sin credencial válida; 403 sin permiso; 404 inexistente o fuera del ámbito; 409 conflicto; 413 y 415 tamaño o tipo; 422 regla de negocio (`RN-xx` en Reconstrucción); 429 límite; 503 degradada, con hora de reintento. En gRPC: `FAILED_PRECONDITION` por regla, `PERMISSION_DENIED`, `NOT_FOUND`, `ABORTED` por cambio simultáneo e `INVALID_ARGUMENT`.

| Unidad | Operaciones construidas | Requisitos |
| --- | --- | --- |
| Núcleo | `/auth/login`, `/auth/me`, `/auth/logout` y `/auth/usuarios` (la app y los scripts los usan como `/api/auth/...` a través de `servidor`). `GET /eventos`, `GET /eventos/{id}` y su `/historial`. `PATCH /eventos/{id}/gravedad`, `/fase` y `/caracterizacion`. Registro del desastre. Catálogo interno `/integracion/catalogo`, con clave de integración. Inicio de sesión SOAP y EJB remoto | FC-07; RF-GD-01, 03, 05, 06, 07, 13, 14 |
| `servidor` | `GET /api/desastres`, `/api/catalogos`; `POST /api/campo/lotes`; `GET` y `PUT /api/campo/evaluaciones/{id}`, con `/historial`, `/foto` y `/desastre`; `GET /api/panorama`; `/api/salud` | RF-ED-01 a 03, 07, 08, 11 a 14 |
| Reconstrucción REST | Bajo `/api/reconstruccion`: desastres (iniciar reconstrucción, cerrar, evaluaciones, seguimiento, auditoría) y proyectos (crear, aprobar, iniciar, presupuesto, avance, costo, evidencia, estado, auditoría) | RF-RE-01 a 06 |
| Reconstrucción gRPC | `ReconstruccionService`, 14 RPC: `IniciarReconstruccion`, `CrearProyecto`, `CrearProyectosEnLote`, `AprobarProyecto`, `IniciarEjecucion`, `RegistrarAvance`, `RegistrarCosto`, `AdjuntarEvidencia`, `ModificarPresupuesto`, `CambiarEstado`, `ConsultarSeguimiento`, `ConsultarAuditoria`, `CerrarDesastre`, `ConsultarEvaluaciones` | RF-RE-01 a 06 |
| Portal | Páginas MVC y `GET /tablero?desastre=` (JSON del mapa) | Mínimo c |
| Notificaciones | `POST /desastres` (aviso desde la bandeja de salida); `POST` y `GET /sistemas` (suscripción) | RF-GD-13, RF-CO-06 |

Diseñadas, sin código: validación y duplicados de evaluaciones, documentos PDF, delimitación y consulta histórica, consolidación, participación y tareas, tipos de desastre, auditoría consolidada, consulta pública y alertas por fuente.

| Contrato entre unidades | Qué fija | Estado |
| --- | --- | --- |
| C1 Tipos de activo | Nueve códigos (vivienda, edificio público, escuela, centro de salud, vía, puente, red de agua, red de energía, otro), obligatorios en cada evaluación | En curso; Reconstrucción usa por ahora el texto del activo |
| C2 Caracterización | `PATCH /eventos/{id}/caracterizacion` (§5.3) | Construido en el núcleo; reenvío desde `servidor` y la app en curso |
| C3 Resumen de evaluaciones | Totales por nivel y por tipo de activo | Opcional: el tablero lo arma con `/api/panorama` |
| C4 Sesión | Token Bearer validado con `/auth/me`; 401 vencido, 403 sin permiso | Construido |
| C5 Eventos de Reconstrucción | `idEvento` (también es el id del mensaje AMQP), `tipo`, `ocurridoEn`, `desastreId` (el UUID que usa Reconstrucción), `proyectoId`, `organismoId` y `datos`; intercambio `reconstruccion.eventos`, cola `integracion.reconstruccion` | Publicación construida; consumo en curso |

| Aviso | Origen y canal | Estado |
| --- | --- | --- |
| `DESASTRE_REGISTRADO`, `DESASTRE_GRAVEDAD_CAMBIADA` | Núcleo → bandeja de salida → notificaciones → cola por suscriptor (`sistema.<id>`) y correo | Construido |
| `PROYECTO_CREADO`, `PROYECTO_APROBADO`, `PROYECTO_ESTADO_CAMBIADO`, `PROYECTO_CON_SOBRECOSTO`, `PRESUPUESTO_MODIFICADO`, `DESASTRE_EN_RECONSTRUCCION`, `DESASTRE_CERRADO` | Reconstrucción → bandeja de salida → C5 | Publicación construida; consumo en curso |
| Alerta externa | SE-01 → intercambio `desastres.entrada`, cola `desastres.registro` | Cola construida; adaptadores diseñados |
| Fase cambiada por el coordinador, delimitación, tarea, sistema externo caído | Núcleo y UD-08 | Diseñado |

Cada cola tiene su `<cola>.reintento`, que devuelve el mensaje a los 5 s, y su `<cola>.errores`, visible para un operador. Los mensajes son JSON persistentes con identificador único.

**Garantías de entrega.** *Sin pérdida:* el aviso se guarda con su cambio y se entrega hasta que se acepta. *Al menos una vez:* un aviso puede repetirse y el destinatario lo descarta por su identificador. *Orden no garantizado:* cada aviso lleva el recurso y su versión, y el destinatario conserva la más alta. *Capacidad:* 10 alertas críticas simultáneas, porque el modelo de carga supone menos de 10 mensajes por segundo de todos los externos; por encima, se atiende por severidad y la espera crece, como degradación declarada. *Evolución:* dentro de una versión solo se agregan campos opcionales.

### 8.5 Operación sin conexión

- La app exige un primer inicio de sesión con conexión y guarda el token en el almacenamiento seguro del dispositivo.
- SQLite es la fuente de verdad local. Catálogo y capturas se separan por organismo, y las capturas también por usuario.
- Sin conexión se captura y se corrige. Cada operación lleva un identificador del dispositivo y la versión base (ADR-04). Ante un conflicto, el evaluador elige y su decisión es una operación nueva.
- El dispositivo no borra una captura hasta la confirmación. Revocar la sesión de un dispositivo perdido impide sincronizar, pero no borra lo guardado; por eso solo guarda catálogos y capturas del propio usuario. **Limitación:** el SQLite no va cifrado.
- **En curso:** sincronizar al recuperar la red, al volver a la app y al guardar, con una franja de estado y pendientes; tratar el 401 aparte de la falta de red y reanudar tras un nuevo inicio de sesión, porque un evaluador puede pasar más de 8 horas sin señal; elegir el tipo de activo (C1); y caracterizar el desastre desde la app (C2).
- **Evidencia:** `verificar_android_instrumentado.py` ejecuta en el emulador la desconexión real, la persistencia, el conflicto y su resolución.

### 8.6 Concurrencia y consistencia

- **Identificador del cliente y versión base.** Repetir una escritura no duplica; el mismo identificador con otro contenido, o una versión que ya no es la vigente, responde 409 sin cambiar nada.
- **Transacción local.** El cambio, su historial, su auditoría y su aviso van juntos en la base de la unidad dueña. No hay transacciones entre unidades.
- **Bloqueo del desastre.** Todo cambio de nivel, fase o caracterización bloquea la fila del desastre antes de comparar la versión.
- **Reclamo de avisos.** La bandeja del núcleo reclama con `FOR UPDATE SKIP LOCKED` y un vencimiento de 30 s; el envío HTTP a notificaciones ocurre fuera de la transacción, y si falla, el aviso vuelve a estar disponible al vencer el reclamo. Reconstrucción y los lotes diferidos de `servidor` usan el mismo bloqueo, así que varias instancias no publican dos veces lo mismo.
- **Reconstrucción.** La base decide entre dos proyectos simultáneos sobre la misma evaluación (RN-09), y el repositorio rechaza la segunda escritura sobre una versión aunque el cliente no envíe `If-Match`.
- **Consistencia eventual.** Las copias de lectura van segundos atrás (ADR-03); el tablero acota su concurrencia con un pool fijo (ADR-24).

## 9. Decisiones arquitectónicas

Cada ADR indica su estado en el código. Frente a la versión 1.0, ADR-01, 02, 03, 10, 11, 14, 17, 19, 20 y 22 se actualizan con lo construido, y ADR-23 a 29 son nuevas.

#### ADR-01 — Unidades de despliegue por consistencia y perfil de carga · Construido en parte

- **Requisitos.** QA-ESC-01, QA-DIS-01, QA-ESC-02; Escenario B.
- **Contexto.** Hay que crecer solo lo necesario, y el dato, su historial, su auditoría y su aviso deben guardarse juntos.
- **Alternativas.** Monolito modular; núcleo con satélites; servicios por consistencia y carga; un servicio por componente (§4.1).
- **Decisión.** Servicios de grano grueso. El núcleo reúne identidad, desastres y auditoría; lo demás va en unidades aparte. UD-05 se retira (ADR-28).
- **Justificación.** Lo que recibe picos crece solo, y lo consistente queda en una transacción local.
- **Consecuencias.** El núcleo es dependencia de todos. Construidas UD-01, 02, 03, 04, 06 y 09; diseñadas UD-07 y 08.

#### ADR-02 — Comunicación entre unidades · Construido

- **Requisitos.** QA-RES-02, QA-REN-02, QA-REN-03.
- **Contexto.** Una llamada inmediata acopla disponibilidades; una cola no responde en el momento.
- **Alternativas.** Todo inmediato; todo por eventos; inmediato solo cuando se necesita la respuesta.
- **Decisión.** Llamada inmediata con tiempo de espera cuando el usuario espera el resultado: REST entre tecnologías distintas, gRPC entre servicios Java (ADR-26). Colas para avisar, siempre desde una bandeja de salida.
- **Justificación.** El registro se confirma con su resultado y no depende de notificaciones.
- **Consecuencias.** Sin el núcleo, ninguna unidad atiende operaciones protegidas.

#### ADR-03 — Datos por unidad y copias de lectura derivadas · Construido; copia pública diseñada

- **Requisitos.** QA-ESC-01, QA-SEG-02; PREM-03.
- **Contexto.** Miles de consultas compiten con los registros.
- **Alternativas.** Base compartida; lectura pública sobre datos operativos; base por unidad con copias.
- **Decisión.** Ninguna unidad lee la base de otra. `servidor` copia el catálogo de desastres; Reconstrucción, las evaluaciones de origen; la consulta pública tendrá solo lo publicable.
- **Justificación.** La lectura no toca las bases críticas, y se publica por inclusión expresa.
- **Consecuencias.** Consistencia eventual de unos segundos en la copia de `servidor`; las pruebas y la app esperan la sincronización.

#### ADR-04 — Reconciliación tras trabajar sin conexión · Construido

- **Requisitos.** QA-CNF-01, QA-DIS-03; RF-ED-12, RF-ED-13.
- **Contexto.** Lo capturado llega horas después, cuando otros pudieron cambiar los mismos datos.
- **Alternativas.** Identificador del servidor (no funciona sin conexión); última escritura gana (sobrescribe en silencio); operaciones sobre versión base.
- **Decisión.** Cada cambio viaja con un identificador del dispositivo y su versión base; si la vigente es otra, queda como conflicto explícito.
- **Justificación.** Ni sobrescrituras silenciosas ni capturas perdidas.
- **Consecuencias.** El dispositivo guarda más datos y alguien debe resolver los conflictos.

#### ADR-05 — Consolidación de reportes · Diseñado

- **Requisitos.** QA-CNF-02; RF-GD-11, RF-GD-16, RF-ED-10.
- **Contexto.** Varios reportes pueden referirse al mismo evento o activo.
- **Alternativas.** Reemplazar el original (pierde procedencia); mover registros entre bases (sin transacción común); unir por similitud; consolidación gobernada con alias.
- **Decisión.** La similitud solo propone; decide una persona, salvo una regla certificada por dos. Consolidar crea un alias con vigencia hacia un evento canónico abierto que no es alias de otro, sin mover datos. Las consolidaciones simultáneas se resuelven por versión. Revertir cierra el alias.
- **Justificación.** Un error de unión se corrige sin tocar datos.
- **Consecuencias.** Toda consulta por evento resuelve el alias.

#### ADR-06 — Historia con dos fechas por versión · Diseñado

- **Requisitos.** QA-TRZ-02; RF-GD-09, RF-GD-14.
- **Contexto.** El área cambia y los datos de campo llegan tarde.
- **Alternativas.** Solo hora de registro; solo hora de vigencia; ambas.
- **Decisión.** Cada versión de la delimitación y del nivel guarda desde cuándo aplica y cuándo se registró.
- **Justificación.** Distingue qué estaba afectado a las 10:00 de qué se sabía a las 10:00.
- **Consecuencias.** Consultas históricas más costosas y degradables. Hoy hay historial con versión y hora de registro.

#### ADR-07 — Integración por adaptadores · Diseñado; colas de suscriptor construidas

- **Requisitos.** QA-INT-01, QA-INT-02, QA-RES-01, QA-EXT-01; CON-01 a CON-04.
- **Contexto.** Ocho tipos de sistemas con formatos propios que no se pueden cambiar.
- **Alternativas.** Integración directa desde cada servicio; formato impuesto (contradice CON-01); un adaptador por sistema.
- **Decisión.** Un adaptador por sistema en UD-08 y cuatro mecanismos según la necesidad (§3.3).
- **Justificación.** Integrar o cambiar un sistema toca un solo adaptador, y su falla queda en él.
- **Consecuencias.** Un salto más y un modelo común que mantener.

#### ADR-08 — Variabilidad por configuración · Diseñado

- **Requisitos.** QA-MOD-01, QA-MOD-02; RF-GD-04, RF-ED-04 a 06.
- **Contexto.** Tipos, activos, categorías y organismos deben agregarse sin código.
- **Alternativas.** Extensiones con código; configuración con uno o con dos niveles de control.
- **Decisión.** Catálogos como configuración ordinaria, auditada; reglas de seguridad, sesiones y consolidación como configuración de alto impacto, con dos personas.
- **Justificación.** Cambia sin desplegar y sin perder trazabilidad.
- **Consecuencias.** Hay que clasificar cada parámetro. Hoy los catálogos son fijos.

#### ADR-09 — Recuperación ante falla grave · Diseñado

- **Requisitos.** QA-DIS-02.
- **Contexto.** Recuperar lo crítico en 15 minutos sin perder lo confirmado.
- **Alternativas.** Copia periódica con restauración manual; centro en espera con réplica diferida; tres centros con réplica síncrona.
- **Decisión.** La pérdida de un centro se resuelve sola en 5 minutos; la de dos centros o una corrupción exige dos personas (§8.2).
- **Justificación.** Las otras alternativas pierden lo último confirmado.
- **Consecuencias.** Costo de tres centros; las fallas mayores pueden perder datos.

#### ADR-10 — Seguridad y control de acceso · Construido; modo degradado diseñado

- **Requisitos.** QA-SEG-01, QA-SEG-02, QA-TRZ-01; FC-07; CON-09.
- **Contexto.** Varios organismos comparten la plataforma y un dispositivo puede perderse.
- **Alternativas.** Credencial firmada sin consultar identidad (revocación lenta); proveedor externo (dependencia ajena); caché de validaciones (retrasa la revocación); sesión propia validada en el núcleo.
- **Decisión.** Token de 8 horas validado contra el núcleo por cada unidad en cada solicitud; usuario, rol y organismo salen de esa validación (§8.1). Solo sin núcleo, `servidor` usaría la última validación por 15 minutos.
- **Justificación.** Revocación inmediata; el organismo no se falsifica desde el cliente.
- **Consecuencias.** Un salto al núcleo por operación; sin él nadie inicia sesión. Se mitiga con redundancia.

#### ADR-11 — Protección de las funciones críticas · Separación construida; redundancia diseñada

- **Requisitos.** QA-DIS-01, QA-RES-02.
- **Contexto.** El SRS clasifica las funciones en críticas, esenciales y degradables.
- **Alternativas.** Tratar todo igual; separar dentro de una unidad; separar lo degradable en unidades aparte.
- **Decisión.** Lo crítico vive en entrada, portal, núcleo, `servidor` y notificaciones, con capacidad sobrante y en los tres centros. Reconstrucción y consulta pública van aparte. El desastre se confirma antes de notificar.
- **Justificación.** Medido: sin Reconstrucción, el tablero sigue respondiendo.
- **Consecuencias.** Capacidad ociosa en operación normal.

#### ADR-12 — Tres centros de datos con réplica síncrona · Diseñado

- **Requisitos.** QA-DIS-01, QA-DIS-02.
- **Contexto.** 99,95 % mensual son 21,6 minutos de caída.
- **Alternativas.** Un centro redundante; dos centros, activo y en espera; tres centros.
- **Decisión.** Tres centros independientes; cada base con primario y dos réplicas síncronas, confirmando con una; RabbitMQ en tres nodos.
- **Justificación.** Cálculo ilustrativo de §7.2.
- **Consecuencias.** Cerca de 0,8 s por escritura crítica; los centros no deben compartir dependencias.

#### ADR-13 — La fotografía viaja con su evaluación · Construido

- **Requisitos.** QA-CNF-01, QA-ESC-02; RF-ED-07.
- **Contexto.** Una evaluación lleva una foto de hasta 5 MB capturada sin conexión.
- **Alternativas.** Canal aparte con carga reanudable; foto en la misma operación.
- **Decisión.** La foto viaja con su evaluación y se guarda con ella; primero van los lotes sin foto.
- **Justificación.** No hay evaluación sincronizada con su evidencia perdida, y la foto hereda sus permisos.
- **Consecuencias.** Un corte obliga a reenviar todo el paquete. Falta medir con fotos grandes y redes reales.

#### ADR-14 — Entrada única · Construido

- **Requisitos.** QA-ESC-01, QA-SEG-01.
- **Contexto.** Varios clientes entran a varias unidades.
- **Alternativas.** Acceso directo; una entrada por cliente; entrada única.
- **Decisión.** Nginx enruta por prefijo hacia el portal, `servidor` y Reconstrucción (§7.1), y limita el tamaño de las solicitudes y los intentos de inicio de sesión. El límite del portal público queda diseñado con UD-07.
- **Justificación.** Las unidades no se exponen.
- **Consecuencias.** Todo pasa por ella; por eso no guarda estado.

#### ADR-15 — Notificación por dos canales · Construido

- **Requisitos.** QA-REN-03; RF-GD-13, RF-CO-06.
- **Contexto.** Una alerta debe llegar a sistemas y a personas, y cualquier destino puede fallar.
- **Alternativas.** Un canal con reintentos; dos canales independientes.
- **Decisión.** Cola propia por sistema suscrito, más correo; cada canal con cola de reintento y de errores.
- **Justificación.** Un suscriptor caído no retrasa a los demás.
- **Consecuencias.** Avisos repetidos, que se descartan por identificador.

#### ADR-16 — Monitoreo activo · Solo sondas de salud

- **Requisitos.** QA-RES-02, QA-ESC-01.
- **Contexto.** Detectar en 10 s y crecer en 5 minutos exige medir siempre.
- **Alternativas.** Reportes de usuarios; sondas; sondas con métricas.
- **Decisión.** Sonda cada 5 s y métricas de uso, latencia y colas, que disparan reemplazo, crecimiento y conmutación.
- **Justificación.** Sin medición continua no hay reacción automática.
- **Consecuencias.** El monitoreo también debe ser redundante.

#### ADR-17 — Tecnologías de la implementación · Construido

- **Requisitos.** CON-07, CON-02; restricciones del curso (§2.2).
- **Contexto.** Los talleres usaron Jakarta EE, Spring Boot, .NET, mensajería, REST, SOAP, gRPC y llamadas remotas; el curso pide MVC, servlets multihilo y .NET sin conexión.
- **Alternativas.** Una plataforma para todo; la que mejor se ajusta a cada unidad.
- **Decisión.** Núcleo: Java 21 y Jakarta EE en WildFly 41 (EJB, JPA, JTA, JAX-RS, SOAP, EJB remoto). Portal: Java 21, Spring Boot 3.3, Spring MVC, Thymeleaf y servlets en Tomcat, con cliente gRPC. Reconstrucción: Java 17, Spring Boot 3.3, REST y gRPC. `servidor`: ASP.NET Core 10. App: .NET MAUI y SQLite. Notificaciones: Java 17 y Jakarta EE en Payara 6. Además, PostgreSQL 17, RabbitMQ 4.1, Nginx 1.28 y Docker Compose.
- **Justificación.** JTA da la transacción local de ADR-01; Java para el *back-office* en línea y .NET para lo desconectado. CON-02 se ejercita con REST, gRPC, SOAP y EJB remoto.
- **Consecuencias.** Dos plataformas y tres servidores de aplicaciones; la copia del `.proto` en el portal debe igualar la de Reconstrucción.

#### ADR-18 — Incorporación inmediata o diferida de los lotes · Construido

- **Requisitos.** QA-ESC-02.
- **Contexto.** `servidor` puede aplicar cada lote al recibirlo o después.
- **Alternativas.** Inmediata con resultado por operación; diferida con acuse.
- **Decisión.** Ambas, con el mismo contrato, elegidas por configuración (`CAMPO_DIFERIDA`); por defecto, inmediata.
- **Justificación.** La inmediata confirma en la llamada; la diferida absorbe picos.
- **Consecuencias.** En diferido, el evaluador no ve los conflictos de inmediato.

#### ADR-19 — Ámbito de acceso por organismo y por evento · Organismo construido; evento diseñado

- **Requisitos.** QA-SEG-02; RF-CO-04; RF-GD-12.
- **Contexto.** Hay que aislar organismos y, a la vez, compartir en una emergencia.
- **Alternativas.** Todo por organismo; todo visible; ámbitos según el dato.
- **Decisión.** Ámbito de organismo y ámbito de evento (participación vigente), con el organismo de la sesión. El ámbito de caso de la versión 1.0 deja de aplicar (ADR-28).
- **Justificación.** Se comparte solo lo que la coordinación necesita.
- **Consecuencias.** Cada consulta comprueba la participación.

#### ADR-20 — Auditoría local por unidad con consulta consolidada · Auditoría local construida

- **Requisitos.** QA-TRZ-01; RF-RE-04.
- **Contexto.** No debe haber cambio sin registro, y no hay transacciones entre bases.
- **Alternativas.** Almacén único escrito por llamada; auditoría local; local con copia consolidada.
- **Decisión.** Cada unidad audita en su base, en la transacción del cambio, y envía una copia que SRV-12 reúne.
- **Justificación.** Si existe el cambio, existe su registro.
- **Consecuencias.** La consulta consolidada va segundos atrás. Construida en el núcleo y en Reconstrucción; envío y consolidación diseñados.

#### ADR-21 — Operaciones externas con confirmación explícita · Diseñado

- **Requisitos.** QA-RES-01; RF-RE-03; CON-04.
- **Contexto.** Comprometer un presupuesto ante SE-06 depende de un sistema que puede no responder y no comparte transacción.
- **Alternativas.** Llamar dentro de la operación; darla por hecha; darla por rechazada al vencer el plazo; dejarla pendiente y conciliar.
- **Decisión.** Por confirmar hasta que el externo responde. Sin respuesta en el plazo, En conciliación: nada se libera ni se revierte, y se consulta por identificador; «no encontrada» no es definitivo. Si no hay consulta ni cancelación, decide una persona con motivo.
- **Justificación.** Si el externo la aplicó, darla por rechazada dejaría los dos lados distintos.
- **Consecuencias.** Estados intermedios visibles; cada contrato necesita consulta, cancelación o plazo de validez.

#### ADR-22 — Registro de entregas en base de datos · Diseñado

- **Requisitos.** QA-DIS-01, QA-DIS-02, QA-REN-03.
- **Contexto.** Notificaciones registra sus entregas en un archivo con un solo escritor.
- **Alternativas.** Archivo con instancia en espera; base replicada; sin registro.
- **Decisión.** Base propia y replicada; cada instancia toma avisos con bloqueo por fila, como ya hacen las bandejas del núcleo y de Reconstrucción.
- **Justificación.** Varias instancias y un registro que sobrevive a un centro.
- **Consecuencias.** Una base más; mientras tanto, una sola instancia activa. Se decide con ADR-27.

#### ADR-23 — Una sola presentación web MVC con Spring MVC y Thymeleaf · Construido

- **Requisitos.** Restricción del curso (MVC con framework); QA-SEG-01, QA-ESC-01; mínimos a, b, c y d.
- **Contexto.** Había HTML estático en `servidor`, React en Reconstrucción y la app MAUI. Ninguna web usaba un framework MVC, y cada una manejaba la sesión en el navegador.
- **Alternativas.** JSF del Taller 4 (estado de vista por sesión); ASP.NET Core MVC (sin servlets); Spring MVC con Thymeleaf; dejar HTML estático y React.
- **Decisión.** UD-09 Portal web, en Spring Boot 3.3 con Spring MVC y Thymeleaf, como única interfaz web (§5.2.2). Habla por REST con el núcleo y `servidor`, y por gRPC con Reconstrucción.
- **Justificación.** Un solo inicio de sesión, con el token guardado en el servidor. Sin estado de vista, a diferencia de JSF, lo que favorece el Escenario B. Corre en un contenedor de servlets, lo que permite ADR-24. No agrega tecnología.
- **Consecuencias.** A favor: se verifica de punta a punta (`verificar_portal.py`, 44 comprobaciones con tres roles) y gRPC tiene un cliente real. En contra: depende de tres servicios, repite algunas validaciones y su sesión en memoria pide afinidad con varias instancias.

#### ADR-24 — Servlet asíncrono y multihilo para el tablero · Construido y medido

- **Requisitos.** Restricción del curso (multihilo con servlets); QA-REN-01, QA-RES-02, QA-ESC-01; mínimo c.
- **Contexto.** El tablero junta tres dueños. En secuencia, sus latencias se suman y una falla tumba la página.
- **Alternativas.** Consultas secuenciales en el controlador; paralelas desde el navegador; servlet asíncrono con pool acotado.
- **Decisión.** `TableroServlet` con `@WebServlet(asyncSupported = true)`, `startAsync` y un `ThreadPoolExecutor` de 16 hilos con cola de 64; 1,5 s por fuente, respuesta parcial y 503 con el pool lleno (§6.6).
- **Justificación.** Sin carga tarda lo de la fuente más lenta; una fuente caída no tumba el tablero; el pool limita la presión sobre los servicios. Las tres cosas están medidas (§6.6).
- **Consecuencias.** Trade-off medido: con 20 clientes en una sola máquina el paralelo no gana (p95 de 241 frente a 233 ms), porque los servicios de atrás ya están saturados. Puntos de sensibilidad: el tamaño del pool, la cola, el límite por fuente y la vigencia de la copia del catálogo en Reconstrucción.

#### ADR-25 — Operación sin conexión con .NET MAUI y SQLite · Construido; mejoras en curso

- **Requisitos.** Restricción del curso (componentes .NET para desconectados); QA-DIS-03, QA-CNF-01; mínimo a.
- **Decisión.** La app de campo es .NET MAUI con SQLite como fuente de verdad local. Sincroniza por lotes idempotentes al recuperar la red y muestra los conflictos de versión sin sobrescribirlos.
- **Contexto, alternativas, justificación y consecuencias.** Pendiente: texto de Rol 2.

#### ADR-26 — Reconstrucción en Spring Boot con REST y gRPC interno · Construido

- **Requisitos.** Mínimo b; QA-INT-01, QA-TRZ-01; CON-02.
- **Contexto.** El diseño del taller era .NET, pero el curso deja .NET para lo desconectado. Reconstrucción es un *back-office* en línea que comparte con el núcleo la identidad y la fase del desastre.
- **Alternativas.** .NET con gRPC y SignalR, como en el taller; Jakarta EE dentro del núcleo; Spring Boot aparte.
- **Decisión.** Unidad propia en Spring Boot 3.3 con su base. REST para clientes heterogéneos y scripts; gRPC (14 RPC) como canal interno del portal. Valida el token contra el núcleo y publica desde una bandeja de salida con el formato C5.
- **Justificación.** Mismo lenguaje que el núcleo y el portal; reglas RN-01 a RN-21 en un dominio sin framework, probado en memoria; contrato gRPC tipado entre servicios Java.
- **Consecuencias.** Una tercera base y un tercer servidor de aplicación. El refresco del tablero reemplaza el tiempo real (SignalR) del taller. No guarda datos de personas.

#### ADR-27 — Consumo continuo de RabbitMQ en notificaciones · En curso

- **Requisitos.** QA-REN-03 (10 alertas simultáneas en menos de 5 s); QA-RES-02.
- **Decisión.** Notificaciones consume cada cola de forma continua, en lugar de tomar un mensaje por segundo con un temporizador, y consume también la cola `integracion.reconstruccion` (C5).
- **Contexto, alternativas, justificación y consecuencias.** Pendiente: texto de Rol 2.

#### ADR-28 — Personas afectadas, recursos y donaciones como sistemas externos · Aplicado

- **Requisitos.** Enunciado §2, que nombra esos sistemas como existentes; CON-01; indicación del profesor en clase.
- **Contexto.** La versión 1.0 diseñaba UD-05 (personas) y SRV-08 (recursos y donaciones), sin código.
- **Alternativas.** Construirlas; dejarlas diseñadas dentro; tratarlas como sistemas externos.
- **Decisión.** Son sistemas externos (SE-04 y SE-05) con su punto de integración. UD-05 se retira sin renumerar. Las Solicitudes de recursos del Taller 2 salen del portal; su código sigue en el núcleo y en `servidor` hasta que el equipo acuerde retirarlo. Reconstrucción no registra personas ni número de afectados.
- **Justificación.** Coincide con el enunciado y con el profesor, y concentra el esfuerzo en los mínimos.
- **Consecuencias.** RF-PA y RF-GR pasan a fuera de alcance en el SRS; el ámbito de caso deja de aplicar.

#### ADR-29 — Metas de rendimiento con percentiles medidos · Aplicado

- **Requisitos.** QA-REN-01 a QA-REN-03; anotación del profesor sobre los escenarios.
- **Contexto.** El enunciado da tiempos sin decir cómo medirlos, y el promedio esconde las solicitudes lentas.
- **Alternativas.** Promedio; máximo; percentiles.
- **Decisión.** p50 y p95, medidos en el ambiente entregado con el número de solicitudes y la concurrencia. Las metas de los tres centros son metas de diseño.
- **Justificación.** Bass, Clements y Kazman miden latencia y tasa de incumplimiento: «p95 menor que 2 s» es una latencia máxima con hasta 5 % de incumplimiento. El libro de SRE de Google (Beyer et al., 2016, cap. 4) trata las latencias como distribuciones.
- **Consecuencias.** No se extrapola al despliegue objetivo. Evidencia: `portal/scripts/medir_tablero.py` y su informe JSON.

## 10. Requisitos de calidad

### 10.1 Árbol de calidad

Los escenarios completos están en el SRS. Aquí se ordenan por importancia (I) y dificultad (D): A alta, M media, B baja.

| Atributo | Refinamiento | Escenario del SRS | (I, D) |
| --- | --- | --- | --- |
| Disponibilidad | Servicio continuo de lo crítico | QA-DIS-01 | (A, A) |
| Disponibilidad | Recuperación tras perder un centro | QA-DIS-02 | (A, A) |
| Disponibilidad | Datos de campo sin pérdida | QA-DIS-03 | (M, M) |
| Escalabilidad | Usuarios e ingesta de campo | QA-ESC-01, QA-ESC-02 | (A, A) |
| Rendimiento | Consulta, registro y alerta | QA-REN-01 a QA-REN-03 | (A, M) |
| Resiliencia | Falla externa y falla interna | QA-RES-01, QA-RES-02 | (A, M) |
| Interoperabilidad y extensibilidad | Intercambio, cambio de contrato, sistema nuevo | QA-INT-01, QA-INT-02, QA-EXT-01 | (A, M); (M, M); (M, M) |
| Seguridad | Confidencialidad y separación entre organismos | QA-SEG-01, QA-SEG-02 | (A, M) |
| Trazabilidad | Operaciones críticas; evolución del evento | QA-TRZ-01, QA-TRZ-02 | (A, B); (M, M) |
| Modificabilidad | Tipo de desastre y organismo | QA-MOD-01, QA-MOD-02 | (M, B) |
| Confiabilidad | Reconciliación; consolidación | QA-CNF-01, QA-CNF-02 | (A, A); (M, M) |

### 10.2 Meta, enfoque, decisión y evidencia

| Meta | Enfoque | ADR | Evidencia al 15-nov-2026 |
| --- | --- | --- | --- |
| QA-DIS-01 | Redundancia de lo crítico; lo degradable aparte | 01, 11, 12 | Diseño; el tablero sigue sin Reconstrucción (medido) |
| QA-DIS-02 | Réplica síncrona y conmutación | 09, 12 | Ninguna: sin código |
| QA-DIS-03, QA-CNF-01 | Operaciones sobre versión base; almacén local | 04, 13, 25 | `verificar_android_instrumentado.py`, `verificar_evolucion.py` |
| QA-ESC-01 | Unidades que crecen por separado; entrada única; portal sin estado de vista | 01, 03, 14, 23 | Modelo de capacidad (estimación) |
| QA-ESC-02 | Unidad propia para campo; fotos después; incorporación diferida | 13, 18 | `verificar_campo.py`; sin prueba con 500 equipos |
| QA-REN-01 | Copias de lectura; consultas en paralelo | 03, 24, 29 | Tablero: p95 de 44 ms con 1 cliente y de 241 ms con 20 |
| QA-REN-02 | Presupuesto por tramo; resultado en la misma llamada | 02, 29 | 22 ms con poca carga (versión 1.0) |
| QA-REN-03 | Bandeja de salida; cola por suscriptor; consumo continuo | 02, 15, 27 | 2,7 s y 3,2 s con avisos aislados (versión 1.0); 10 simultáneas pendiente |
| QA-RES-01 | Adaptador con corte de circuito y cola | 07, 21 | Cola por suscriptor y bandeja de salida verificadas |
| QA-RES-02 | Procesos separados; respuesta parcial | 11, 16, 24 | 60 de 60 respuestas parciales con Reconstrucción caída |
| QA-INT-01, QA-INT-02, QA-EXT-01 | Adaptadores; REST, gRPC, SOAP y EJB remoto | 07, 17, 26 | Prueba gRPC con `grpcurl`; prueba de EJB remoto; suscripción de sistemas |
| QA-SEG-01, QA-SEG-02 | Sesión validada en el núcleo; permisos en el backend | 10, 19, 23 | `verificar_seguridad.py` (10), `verificar_portal.py` (43), `verificar_unificacion.py` |
| QA-TRZ-01 | Auditoría en la transacción del cambio | 20 | `verificar_reconstruccion.py`, `verificar_caracterizacion.py` |
| QA-TRZ-02, QA-MOD-01, QA-MOD-02, QA-CNF-02 | Dos fechas; configuración; alias | 05, 06, 08 | Sin código |

## 11. Riesgos y deuda técnica

| Riesgo o deuda | Impacto | Estado | Acción |
| --- | --- | --- | --- |
| Tres centros sin montar (réplicas, promoción y bloqueo del primario) | QA-DIS-01 y QA-DIS-02 solo tienen sustento de diseño | Aceptado | Montarlo y hacer el simulacro |
| Modo degradado de `servidor` sin código | Sin núcleo, `servidor` rechaza todo, incluido el mapa | Abierto | Implementarlo; mientras tanto, redundancia del núcleo |
| El núcleo valida cada sesión | Si se satura o cae, lo sienten todas las unidades | Abierto | Redundancia; medir la validación bajo carga |
| UD-07 Consulta pública y UD-08 Integración sin código | La parte ciudadana del Escenario B y los adaptadores de A y C solo están en diseño | Aceptado | Empezar por SE-01, SE-02 y SE-06 |
| Notificaciones toma un mensaje por segundo por cola y guarda las entregas en un archivo | No cumple 10 alertas simultáneas; una sola instancia | Abierto, en curso | ADR-27 y ADR-22 |
| El servicio de notificaciones no autentica a quien lo llama | Dentro de la red de Docker, cualquiera podría publicar o suscribirse | Abierto, en curso | Clave de servicio en sus rutas |
| SQLite del móvil sin cifrar | Un dispositivo perdido expone las capturas de su usuario | Aceptado como limitación | SQLCipher |
| Clave EJB como argumento de construcción del núcleo | Queda en el historial de la imagen | Aceptado para el laboratorio | Pasarla en tiempo de ejecución |
| Código de Solicitudes de recursos en el núcleo y en `servidor` | Mantener y exponer algo fuera de alcance | Abierto | Retirarlo de ambos lados, con sus pruebas, cuando el equipo lo acuerde |
| Catálogo de `servidor` con consistencia eventual de unos segundos | Una evaluación de un desastre recién creado queda preliminar unos segundos | Aceptado | Documentado; las pruebas esperan |
| Trade-off del tablero en una máquina saturada | Más carga simultánea sobre los servicios de atrás | Aceptado | Ajustar pool y límite; medir con servicios escalados |
| Sesión HTTP del portal en memoria | Varias instancias exigen afinidad o almacén compartido | Abierto | Afinidad en la entrada o sesión compartida |
| Sin pruebas de carga de 10.000 usuarios ni de 500 equipos con fotos | Metas de escala sin evidencia | Abierto | Prueba con red real |
| Validación de evaluaciones sin código | Reconstrucción toma como validada una evaluación vinculada a un desastre | Abierto | Validación por un coordinador distinto |
| C1 (tipo de activo) y C2 desde la app, en curso | Proyectos clasificados con texto libre; la app aún no caracteriza | En curso | Ajustar el adaptador de Reconstrucción al publicarse C1 |
| Un cambio de fase del coordinador no emite aviso | Los organismos no se enteran de un cierre directo | Abierto | Emitirlo desde la bandeja de salida |
| Sin TLS ni cifrado en reposo; puertos de prueba en 127.0.0.1 | Datos en claro dentro de la máquina | Aceptado para el laboratorio | Despliegue objetivo; cerrar el puerto del núcleo tras C2 |
| Catálogos fijos; consolidación, dos fechas, coordinación y auditoría consolidada sin código | QA-MOD-01, QA-CNF-02 y QA-TRZ-02 solo en diseño | Abierto | ADR-05, 06, 08, 19 y 20 |
| Mosaicos del mapa desde OpenStreetMap | Sin internet, el mapa no muestra fondo | Aceptado | Servidor de mosaicos propio si se requiere |

## 12. Glosario

| Término | Significado |
| --- | --- |
| Fase del desastre | Registro, Caracterización, Reconstrucción o Cierre |
| Caracterización | Ubicación, radio, nivel y descripción de un desastre (C2) |
| Nivel 1 a 4 | Clasificación del desastre por impacto; distinta del nivel de afectación 1 a 5 de una evaluación |
| Función crítica, esencial o degradable | Clasificación del SRS según lo que pasa si la función falta |
| Unidad de despliegue (UD) | Proceso que se despliega, crece y falla por separado, dueño de su base |
| Componente (SRV) | Responsabilidad lógica; varios pueden vivir en una unidad |
| Respuesta parcial | Respuesta del tablero que trae las fuentes disponibles y nombra la que faltó |
| Aviso | Mensaje que anuncia un cambio ya guardado y viaja por una cola |
| Bandeja de salida | Tabla donde el cambio y su aviso se guardan en la misma transacción |
| Versión base | Versión que el cliente tenía cuando hizo el cambio |
| Clave de idempotencia | Identificador del cliente que evita aplicar dos veces una solicitud repetida |
| Ámbito de acceso | Alcance de un permiso: organismo o evento |
| Operación externa | Operación que queda Por confirmar hasta que un sistema externo responde |
| En conciliación | Operación externa sin resultado conocido: no se revierte y se consulta hasta tener un resultado definitivo |
| Adaptador | Pieza de UD-08 que traduce entre un sistema externo y el modelo común |
| Corte de circuito | Deja de llamar a un sistema que falla y lo prueba cada cierto tiempo |
| Réplica síncrona | Copia en otro centro que confirma un cambio antes de responder |
| Copia aislada | Respaldo con versiones, sin acceso desde la operación, para datos corruptos |
| p50 y p95 | Tiempo por debajo del cual queda el 50 % o el 95 % de las solicitudes medidas |
