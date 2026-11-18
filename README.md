# Territorio · integración de los talleres

Código unificado de la plataforma: núcleo de identidad y desastres (Jakarta EE), evaluaciones de campo y mapas (.NET), captura sin conexión (.NET MAUI), notificaciones (Taller 3, RabbitMQ), Reconstrucción física (Spring Boot, REST y gRPC), el **portal web** (ASP.NET Core Razor Pages), que es la única interfaz web, y el **servicio de tablero** (Java), con el servlet asíncrono multihilo que arma el tablero de un desastre.

Estado al 17 de noviembre de 2026: todo compila y `./verificar.sh` pasa completo (271 comprobaciones, registro en `evidencias/rol1-build/2026-11-17_verificar_final.log`). Incluye el portal en .NET, C1 y C2 en `servidor` y en la app, el consumo de los avisos de Reconstrucción en notificaciones y la app probada en un celular real (`evidencias/rol2-campo/`). La arquitectura está en `documentacion/SAD_arc42.md` (v2.2).

## Ejecutar

Con Docker Desktop activo y Python 3:

```sh
./iniciar.sh
```

Portal web: http://127.0.0.1:18085 · Buzón de pruebas: http://127.0.0.1:18026

`iniciar.sh` crea `servidor/.env` a partir de `.env.ejemplo` si no existe. Compose ya no tiene contraseñas escritas: todas salen de ese archivo, que no se versiona.

Acceso de laboratorio inicial: **coordinador_demo** / **Arqui-Local-2026!**. Puedes definir las variables de `.env.ejemplo` en el entorno antes de la primera instalación; el bootstrap nunca cambia la contraseña de un usuario existente. `compose.yaml` está en servidor/: si usas un archivo de variables, pásalo explícitamente con `docker compose --env-file /ruta/.env -f servidor/compose.yaml ...` y exporta las mismas variables para los scripts Python. El entorno es de demostración local; no es un despliegue de producción.

El inicio aplica V001–V009, incluso sobre un volumen existente. Para detener conservando datos: `docker compose -f servidor/compose.yaml stop`. No uses `down -v` para actualizar.

## Portal web (UD-09) y servicio de tablero (UD-10)

Toda la presentación es .NET: la app de campo (MAUI) y el portal web.

- **Portal web en ASP.NET Core Razor Pages** (`portal/`, .NET 10):
  - Cada pantalla es una página: la vista (`Pages/**/*.cshtml`) y su `PageModel` (`*.cshtml.cs`), que expone los datos que la vista enlaza y atiende los formularios (`OnGet`, `OnPost…`). Se documenta como MVVM del lado del servidor (ADR-23).
  - No guarda datos propios: consume los servicios desplegados. Usa REST con el núcleo y con `servidor`, gRPC con Reconstrucción (cliente generado del mismo `reconstruccion.proto`) y REST con el servicio de tablero.
  - El token del núcleo queda en la memoria del portal; el navegador solo tiene la cookie `pgdr.sesion` (HttpOnly). Cada formulario lleva un token antifalsificación (CSRF).
  - Pantallas: desastres (registro, fase y caracterización), tablero de un desastre con mapa, evaluaciones con historial y foto, mapa general, Reconstrucción física completa y usuarios del organismo.
- **Servicio de tablero** (`tablero/`, Java 21, Spring Boot):
  - Su `TableroServlet` (`GET /tablero?desastre=REF` con `Authorization: Bearer`) es asíncrono y multihilo.
  - Consulta en paralelo el núcleo, `servidor` y Reconstrucción (gRPC) con un pool acotado.
  - Responde parcial si una fuente falla y 503 si el pool está lleno. No se publica: solo lo llama el portal, que reenvía el token del usuario.
- **Medición:** `python tablero/scripts/medir_tablero.py` deja el informe en `evidencias/rol1-tablero/`.

## Qué se unió

- **nucleo/**: Java EJB/JPA/JTA y PostgreSQL, a partir de la entrega del compañero. Usuarios, tokens, roles, organizaciones, registro único de desastres, auditoría y outbox transaccional. Se corrigió la comparación de reintentos para distinguir datos con saltos de línea.
- **servidor/**: se conserva el DAL, los mapas, GeoJSON, fotos y sincronización de campo .NET/PostgreSQL. Se valida cada sesión con el núcleo y se filtran consultas y escrituras por organismo. La copia del catálogo sirve para lectura; no crea desastres por separado.
- **desastres/**: reutiliza RabbitMQ, reintentos, deduplicación, suscriptores y SMTP del Taller 3. Su archivo persistente es ahora una proyección de entregas de notificaciones. El núcleo guarda el desastre antes de despachar; su outbox reintenta cuando vuelven el broker o el notificador. La marca de outbox acredita recepción en la cola; la web muestra aparte las confirmaciones de entrega del Taller 3.
- **campo/**: APK 0.4.0 con login compartido, token en almacenamiento seguro, captura y catálogo SQLite separados por organismo y capturas separadas también por usuario. Puede capturar sin conexión después del primer login; sincronizar exige una sesión válida. Una sesión vencida requiere iniciar sesión nuevamente, sin eliminar pendientes.

COORDINADOR administra desastres, suscriptores, usuarios y puntos de coordinación; EVALUADOR captura y vincula evaluaciones; CONSULTA solo lee. Evaluador y organismo efectivos proceden del token, aunque el cliente envíe otros valores. Las fotos y exportaciones tienen los mismos permisos que sus registros. Si identidad está caída, las APIs protegidas rechazan temporalmente el acceso; no se omite la autorización para mantenerlas disponibles. La captura local ya iniciada en Android continúa desconectada.

Los clientes usan `/api/auth/login`, `/api/auth/me`, `/api/auth/logout` y `/api/auth/usuarios`. El resto de las APIs mantiene sus rutas anteriores, con `Authorization: Bearer <token>`. `POST /api/desastres` devuelve 201 al guardar y 200 al repetir exactamente el mismo ID y contenido; 409 indica conflicto. Las notificaciones son posteriores. El puerto 18092 expone la API Java solo en localhost; las rutas internas requieren INTEGRACION_KEY. El Taller 3 queda dentro de la red Docker, sin su anterior puerto público 18081.

## Solicitudes de recursos · Taller 2

> Fuera de alcance según el profesor (gestión de recursos). El portal ya no la ofrece; su código se retirará del núcleo y de servidor cuando el equipo lo acuerde.

La página `solicitudes.html` ya no se sirve: el portal reemplazó la web estática.

Se reutilizan el dominio, los puertos y el EJB SolicitudServicio del ZIP `gestion-emergencias 2.zip`, junto con sus 19 pruebas. La carpeta descomprimida del mismo nombre contenía otra entrega (Taller 3); por eso se usó el ZIP de solicitudes. El almacenamiento en memoria se conserva solo para las pruebas originales y se reemplaza en ejecución por un adaptador JPA/PostgreSQL dentro del núcleo existente.

- Selecciona un desastre de tu organismo, recurso, cantidad positiva y prioridad BAJA/MEDIA/ALTA. El estado inicial siempre es PENDIENTE.
- COORDINADOR y EVALUADOR pueden solicitar; solo COORDINADOR atiende; CONSULTA puede leer. Una atención repetida o concurrente perdedora devuelve 409.
- El listado permite filtrar por desastre y pendientes. La solicitud y su historial sobreviven a los reinicios.
- `solicitudId` es un UUID estable generado por el cliente: repetir el mismo contenido no duplica; cambiarlo con la misma clave devuelve 409. El formulario conserva un intento sin confirmar durante recargas de la misma sesión.
- Atender registra la gestión de una solicitud; no se añadió un inventario físico, reservas ni asignaciones automáticas.

APIs bajo `/api`: POST/GET `/solicitudes`, GET `/solicitudes/pendientes`, GET `/solicitudes/{id}`, PATCH `/solicitudes/{id}/atender`, GET `/solicitudes/{id}/historial`. El POST usa `{solicitudId, eventoId, tipoRecurso, cantidad, prioridad}`. Descripción del desastre y organismo proceden del registro central y la sesión, no del texto enviado por el cliente. Auditoría y transición se guardan en la misma transacción JTA.

Prueba real: `python3 servidor/scripts/verificar_solicitudes.py`. Reinicia núcleo/API; no ejecutar durante una demostración concurrente.

## Evolución conectada de los talleres

- **Gravedad:** el catálogo expone `desastre.version`. PATCH `/api/desastres/{id}/gravedad` recibe `{cambioId,versionBase,severidad,motivo}`. Solo un coordinador cambia la gravedad. Historial, versión y aviso pendiente se guardan en una transacción. GET `/api/desastres/{id}/historial` conserva antes/después, motivo y autor. Cada aviso tiene ID propio y conserva la referencia del desastre y la versión; el catálogo muestra la entrega correspondiente a la versión actual.
- **Evaluaciones:** GET `/api/campo/evaluaciones/{id}` incluye `version` (0 para capturas originales). PUT en esa ruta recibe `{operacionId,versionBase,nivelAfectacion,descripcion,activo,latitud,longitud,motivo}`. La captura original y su foto se conservan; las consultas y el calor leen la versión actual. GET `.../historial` muestra las correcciones. Una base obsoleta devuelve 409 sin sobrescribir. Los reintentos conservan el ID de operación.
- **Origen de solicitudes:** desde el detalle se puede solicitar sobre una evaluación concreta. POST `/api/campo/evaluaciones/{id}/solicitudes` recibe los datos de solicitud y `versionBase`; el servidor añade su ID, versión y copia de los datos. La solicitud conserva esa copia aunque se corrija después la evaluación. El cliente no puede falsificar el origen enviando campos libres al núcleo. Un reintento posterior recupera la solicitud ya creada, incluso si la evaluación avanzó de versión.
- **Android offline:** una evaluación sincronizada puede corregirse sin conexión. La propuesta queda en SQLite con su versión base. Si aparece un conflicto al enviar, se conservan propuesta y datos del servidor. El usuario revisa y elige conservar el servidor o reenviar su propuesta sobre la versión revisada; nunca se fuerza la sobrescritura. La resolución local y el acuse se guardan atómicamente. No se cambia la foto durante estas correcciones.

Pruebas reales: `python3 servidor/scripts/verificar_evolucion.py` (incluye SOAP) y `python3 servidor/scripts/verificar_transaccion_gravedad.py` (fallo de aviso limitado a un evento sintético). No ejecutarlas durante una demostración concurrente.

## SOAP y RPC del login

Se adaptó el patrón del taller de login y se reutilizó su `ConexionEjbRemota`. Ambos adaptadores llaman al mismo AuthService: no duplican cuentas, reglas, contraseñas ni sesiones.

SOAP/WSDL: `http://127.0.0.1:18092/plataforma-desastres/AutenticacionProyectoService/AutenticacionSoap?wsdl`. Operaciones: autenticar(usuario,clave), consultar(token), cerrar(token). Los resultados son JSON dentro de la respuesta SOAP. Un token válido sirve en las APIs REST; cerrar lo revoca en todos los accesos.

Prueba EJB remoto desde un proceso independiente:

```sh
docker compose -f servidor/compose.yaml --profile pruebas run --rm --build prueba-rpc
```

Las variables EJB_USUARIO/EJB_CLAVE configuran el usuario técnico de la conexión y se aplican también como argumentos de compilación del núcleo. No sustituyen el usuario/clave de la plataforma. La prueba verifica login RPC, uso del token en mapas REST y revocación.

## Android

```sh
adb install -r Arqui-Campo-integrada.apk
adb reverse tcp:8084 tcp:18085
```

Servidor en la app: `http://127.0.0.1:8084`. Inicia sesión y descarga el catálogo antes de salir a campo. La actualización conserva SQLite si coincide la firma. Las capturas de versiones previas sin organismo no se asignan silenciosamente: un coordinador puede confirmarlas con «Asignar capturas anteriores a mi organismo». No desinstales para actualizar si necesitas conservar datos.

El código requiere .NET 10, maui-android, Android SDK API 36 y JDK 21. `dotnet build campo/src/Arqui.Campo/Arqui.Campo.csproj -c Release`. La firma del APK es de desarrollo.

## Datos anteriores

En este equipo se respaldaron y migraron **únicamente los datos de laboratorio de nuestra integración previa**, conservando IDs, vínculos, JSON originales y fotos. Hay evidencia en `evidencias/unificacion-migracion.log`; el respaldo local está en `respaldo-previo/` y no se incluye en la entrega ZIP. No se tocaron las bases de los talleres originales ni la carpeta PROYECTO ARQUI.

Para otra copia de la integración antigua, primero respalda sus volúmenes y exporta `estado.json` del notificador. Luego inicia la versión nueva y ejecuta explícitamente:

```sh
python3 servidor/scripts/migrar_legado.py /ruta/estado.json
```

Este paso asigna los datos antiguos sin organismo al organismo inicial de laboratorio. No debe usarse para decidir la propiedad de datos reales de otras organizaciones. Una referencia ocupada por otro contenido detiene la importación; no se sobrescribe.

## Verificación

```sh
./verificar.sh
```

Corre, en orden: API, integración, campo, Reconstrucción (con gRPC), evolución, unificación, caracterización (C2), portal y seguridad. La de seguridad va al final porque agota a propósito el cupo de intentos de inicio de sesión por un minuto. `verificar_unificacion.py` crea datos sintéticos y prueba roles, aislamiento, foto, idempotencia, notificaciones y reinicio; detiene el broker y reinicia servicios de esta integración, por lo que debe ejecutarse sin demostraciones concurrentes. Los correos llegan exclusivamente a Mailpit. Los resultados de versiones anteriores se conservan como evidencia histórica y no acreditan por sí solos la versión actual.

## Límites actuales

Esta unión cubre los mínimos del proyecto: app de campo, Reconstrucción física, mapa y evaluación de daños, con autenticación y autorización. Quedan fuera por alcance las personas afectadas, los recursos y las donaciones. Quedan diseñados pero sin código el modo degradado de servidor y el despliegue en tres centros de datos. La integración operativa usa REST y mensajería con transacciones locales JTA. Los adaptadores SOAP y EJB remoto del login ya están conectados a las mismas cuentas y sesiones; no se añadió SAGA.

La API Java original de afectaciones sigue disponible como parte del trabajo del compañero; la captura oficial web/Android y su mapa se conservan en el DAL de campo .NET. No se copian sus evaluaciones a dos bases. La auditoría de creación de desastres se conserva; no se presenta como auditoría exhaustiva de todas las acciones. Los catálogos de la interfaz y los tipos de activos requieren evolución posterior. El APK ahora captura el activo y permite corregirlo junto al daño y la descripción.


## Prueba Android instrumentada

Se ejecutaron los métodos reales del APK, sin automatizar clics, primero en el emulador de Android Studio. El 17-nov se ejecutaron también en un celular real (Samsung SM-A155M, Android 16) con los casos de C1, C2, 409 y 401, y pasaron 30 comprobaciones. Además se tomaron 18 capturas de la interfaz; todo está en `evidencias/rol2-campo/`. La variante de prueba se compila con `-p:PruebasCampo=true`; tras instalarla, ejecutar `python3 servidor/scripts/verificar_android_instrumentado.py`. El script usa ADB del PATH o la variable ADB, y ANDROID_SERIAL (emulator-5554 por defecto). Detiene brevemente solo la API y la restablece al terminar: no ejecutarlo durante una demostración concurrente.

Usa una base SQLite aislada, prueba la migración sobre una copia de la base anterior y restaura la sesión previamente guardada. Crea datos sintéticos en el servidor. Verifica desconexión real, persistencia, conflicto y resolución. Después se reinstala **Arqui-Campo-integrada.apk**, que es la versión 0.4.0 normal y no contiene el ejecutor de pruebas. La prueba instrumentada no se presenta como una prueba manual completa de cada pantalla ni de cámara/GPS físicos.
