# Módulo de Reconstrucción (UD-06)

Reconstrucción física de la Plataforma para la Gestión de Desastres y Reconstrucción. Cubre los casos de uso CU-01 a CU-09 y las reglas de negocio RN-01 a RN-21, con arquitectura limpia (hexagonal). Solo maneja activos físicos (viviendas, vías, puentes, escuelas…): no guarda datos de personas.

**Tecnología:** Java 17, Spring Boot 3.3, REST y gRPC, PostgreSQL. La justificación está en `DECISIONES_TECNOLOGIA.md` y en el ADR-26 del SAD.

**Dos modos de ejecución:**

- **Integrado** (el que se entrega): corre dentro de la plataforma con `./iniciar.sh` desde `integracion/`. Valida el token contra el núcleo, lee los desastres del núcleo y las evaluaciones de `servidor`, y publica avisos en RabbitMQ. Su interfaz es el portal web (UD-09), que lo llama por gRPC.
- **Independiente** (el taller original): `docker compose up --build` en esta carpeta. Usa datos de ejemplo, la interfaz React de `web/` y las cabeceras `X-Usuario`, `X-Rol` y `X-Organizacion` en lugar del token.

## Cómo probarlo

Solo las reglas (31 pruebas unitarias, dominio y casos de uso en memoria):

```bash
cd backend && mvn test
```

De punta a punta, dentro de la plataforma, desde `integracion/`. Son unas 80 comprobaciones: sesión, fases, reglas, versión esperada, eventos y gRPC con `grpcurl`:

```bash
python servidor/scripts/verificar_reconstruccion.py
```

## Estructura

```text
backend/src/main/java/com/pgdr/reconstruccion/
  dominio/                         capa 1: entidades y reglas, sin framework
  aplicacion/casosdeuso/           capa 2: un caso de uso por clase
  aplicacion/puertos/              capa 2: interfaces hacia afuera
  adaptadores/entrada/rest/        capa 3: API REST y filtro de sesión
  adaptadores/entrada/grpc/        capa 3: servicio gRPC e interceptor de sesión
  adaptadores/salida/persistencia/ capa 3: PostgreSQL, auditoría y bandeja de salida
  adaptadores/salida/plataforma/   capa 3: núcleo (desastres y fase) y servidor (evaluaciones)
  adaptadores/salida/mensajeria/   capa 3: despacho de la bandeja de salida a RabbitMQ
  infraestructura/                 capa 4: configuración
backend/src/main/proto/reconstruccion.proto   contrato gRPC (el portal tiene una copia idéntica)
backend/src/main/resources/schema.sql         tablas
```

**Regla de dependencia:** `dominio` no importa nada de las otras carpetas, y `aplicacion` solo importa `dominio`. Ninguna clase de esas dos capas importa Spring, gRPC ni JDBC.

## Dónde está cada regla

| Regla | Qué exige | Dónde |
| --- | --- | --- |
| RN-01, RN-02 | Se entra a Reconstrucción desde Caracterización, con al menos una evaluación validada con daño moderado o superior | `IniciarReconstruccion` |
| RN-03 a RN-05 | Se cierra desde Reconstrucción, sin proyectos abiertos; Cierre es terminal | `CerrarDesastre` |
| RN-06, RN-18 | Proyectos solo en Reconstrucción; el lote crea los válidos y reporta los demás | `CrearProyectos` |
| RN-07, RN-08 | El proyecto nace de evaluaciones del mismo desastre; destrucción total implica Reconstrucción | `Proyecto.proponer` |
| RN-09 | Una evaluación origina a lo sumo un proyecto activo. Lo garantiza la base: tabla `evaluaciones_tomadas` | `CrearProyectos`, `RepositorioProyectosJdbc` |
| RN-10 | Rol y organismo | `CasoDeUso.autorizar`, `CasoDeUso.cargarProyecto` |
| RN-11 a RN-16 | Datos por estado, presupuesto, avance, sobrecosto, finalizar (100 %, evidencia y sin sobrecosto) y transiciones | `Proyecto`, `EstadoProyecto` |
| RN-17 | Auditoría de cambios y de intentos rechazados | `CasoDeUso.ejecutarAuditado`, `AuditoriaJdbc` |
| RN-19 | El módulo no tiene operaciones de pago; solo publica eventos | `BandejaDeSalidaJdbc` |
| RN-20 | Quien formula un proyecto no puede aprobarlo | `Proyecto.aprobar` |
| RN-21 | Solo el responsable asignado opera el proyecto, y cancelar es solo de la autoridad | `CasoDeUso.exigirResponsableAsignado`, `CambiarEstado` |

**Concurrencia:**
- Control optimista al guardar.
- Además, versión esperada de punta a punta: `If-Match` en REST y `version_esperada` en gRPC. Si otro usuario cambió el proyecto, responde 409 o `ABORTED`.

**Avisos:**
- Usan la bandeja de salida, en la misma transacción del cambio.
- Se despachan a RabbitMQ: intercambio `reconstruccion.eventos`, cola `integracion.reconstruccion`.
- Siguen el contrato C5. Hay ejemplos en `../evidencias/rol1-reconstruccion/ejemplos_eventos_C5.json`.

## API REST (bajo `/api/reconstruccion`)

En modo integrado, cada solicitud lleva `Authorization: Bearer <token>`.

| Método y ruta | Caso de uso |
| --- | --- |
| `GET /desastres` | CU-08 |
| `POST /desastres/{id}/iniciar-reconstruccion` | CU-01 |
| `GET /desastres/{id}/evaluaciones` | CU-02 |
| `POST /desastres/{id}/proyectos` | CU-02 (uno o lote) |
| `POST /proyectos/{id}/aprobar` | CU-03 |
| `POST /proyectos/{id}/iniciar` | CU-04 |
| `POST /proyectos/{id}/avance`, `/costo`, `/evidencia` | CU-05 |
| `POST /proyectos/{id}/presupuesto` | CU-06 |
| `POST /proyectos/{id}/estado` | CU-07 |
| `GET /desastres/{id}/seguimiento`, `GET /proyectos/{id}`, `GET /proyectos/{id}/auditoria`, `GET /desastres/{id}/auditoria` | CU-08 |
| `POST /desastres/{id}/cerrar` | CU-09 |

**Errores:** 422 con el código de la regla (`RN-xx`), 403 por rol u organización, 404 si no existe, 409 por cambio simultáneo o versión vieja, y 400 por una solicitud mal formada.

## gRPC (puerto 9096)

Son 14 RPC: los nueve casos de uso, más `ConsultarEvaluaciones`. El portal web es su cliente. En modo integrado, el token va en el metadato `authorization`.

Ejemplo con `grpcurl` desde la red de Docker, en el ambiente integrado:

```bash
docker run --rm --network arqui-integracion_default -v "$PWD/backend/src/main/proto:/protos" fullstorydev/grpcurl:v1.9.3 -plaintext -import-path /protos -proto reconstruccion.proto -H "authorization: Bearer $TOKEN" -d '{"desastre_id":"<uuid>"}' reconstruccion:9096 pgdr.reconstruccion.v1.ReconstruccionService/ConsultarSeguimiento
```

**Errores:** `FAILED_PRECONDITION` por una regla de negocio, `PERMISSION_DENIED`, `UNAUTHENTICATED`, `NOT_FOUND`, `ABORTED` por cambio simultáneo e `INVALID_ARGUMENT`.

## Estado de verificación (15 de noviembre de 2026)

| Parte | Estado |
| --- | --- |
| Dominio y casos de uso | 31 pruebas unitarias pasan |
| Integrado con la plataforma | `verificar_reconstruccion.py` pasa: sesión, fases protegidas, reglas RN-01 a RN-21, versión esperada, avisos C5 y gRPC (con token, sin token, con token inventado y con un rol sin permiso) |
| Portal como cliente gRPC | `verificar_portal.py` pasa: crear, aprobar a dos manos, ejecutar y finalizar desde el portal |
