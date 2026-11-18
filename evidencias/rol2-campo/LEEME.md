# Evidencia de la app de campo en un celular real (17-nov-2026)

- **Dispositivo:** Samsung Galaxy A15 (SM-A155M), Android 16, conectado por USB. No es un emulador.
- **App:** rama `rol2/campo-caracterizacion`, compilada en Debug para `net10.0-android`. Primero se probó el commit `cae6039` (secciones 1 y 2). Después se volvió a probar el commit `1a26bd3`, que corrige los dos hallazgos (sección 3).
- **Conexión:** el celular llega al servidor por `adb reverse tcp:8084 tcp:18085`. En la app el servidor es `http://127.0.0.1:8084`.
- **Cómo se cortó la red:** la prueba instrumentada detiene el contenedor `api`. En las capturas se quitó el túnel con `adb reverse --remove tcp:8084`, así que el celular dejó de alcanzar el servidor.
- **Cómo se venció la sesión:**
  - La prueba instrumentada revoca el token con un logout desde otra conexión.
  - En las capturas se puso `expira_en` en el pasado en la tabla `sesiones` del núcleo, como si hubieran pasado las 8 horas.

## 1. Prueba instrumentada

Archivo: `2026-11-17_android_instrumentado_SM-A155M.log`.

Corre los métodos reales del APK en el celular, sin tocar la interfaz: `servidor/scripts/verificar_android_instrumentado.py` con la variante `-p:PruebasCampo=true`. Resultado: **30 comprobaciones OK y RESULTADO ANDROID: APROBADO**.

| Caso pedido | Comprobaciones del registro |
|---|---|
| 1. Caracterización sin conexión que persiste en SQLite | "C2: la caracterización sin conexión persiste al reabrir SQLite"; "C2: el fallo de red conserva la caracterización pendiente" |
| 2. Tipo de activo que conserva el texto libre | "C1: los tipos de activo del servidor quedan en SQLite (9)"; "C1: el servidor guarda el tipo PUENTE y conserva el texto libre"; "C1: una captura sin tipo elegido queda como OTRO" |
| 3. Sincronización al volver la red, con C2 actualizando el desastre | "C2: al volver la red la caracterización se sincroniza"; "C2: el servidor muestra fase CARACTERIZACION, v1, ubicación, radio 3,5 km y nivel 2" |
| 4. 409 por versión desactualizada, sin sobrescribir | 5 comprobaciones "409: …": el conflicto VERSION_DESACTUALIZADA, que el servidor no cambia, que la propuesta local se conserva, la resolución explícita como operación nueva y que la versión resuelta llega al servidor |
| 5. 401 sin borrar lo pendiente | "la falta de red no se confunde con sesión vencida" y 5 comprobaciones "401: …": la sesión marcada como vencida, los pendientes que siguen en SQLite, que no se reintenta con el token vencido, el nuevo inicio de sesión y la sincronización sin pérdida |

## 2. Capturas de la interfaz (`capturas/`)

Todo se hizo con la app normal (sin el ejecutor de pruebas), sobre el desastre `DES-CAMPO-1711`.

| Captura | Qué muestra |
|---|---|
| 01 | El catálogo se descarga con conexión: 98 desastres |
| 02, 03 | **Sin conexión:** se llena la caracterización (4.8712, -73.9721, radio 2,5 km, nivel 3), queda "guardada sin conexión (base v0)" y en la lista aparece como *pendiente* |
| 04 | **Sin conexión:** la lista de los 9 tipos de activo, tomada de SQLite |
| 05, 06 | **Sin conexión:** evaluación con tipo *Puente (PUENTE)* y nombre libre "Puente La Esperanza", guardada en el teléfono |
| 07 | La app se cerró a la fuerza y se volvió a abrir sin red. Todo sigue pendiente y el aviso dice "Sin respuesta del sistema central… sin perder capturas", no "sesión vencida" |
| 08 | Al volver la red y sincronizar, la caracterización queda *incorporada*. En el servidor el desastre quedó en fase CARACTERIZACION, v1, con la ubicación, el radio de 2,5 km y el nivel 3. La evaluación quedó con `tipoActivo=PUENTE` y `activo="Puente La Esperanza"` |
| 09, 10 | El catálogo se actualiza (v1) y luego se corta la red. Sin conexión se guarda otra caracterización sobre v1 mientras otro dispositivo cambia el desastre a v2. Al sincronizar queda en *conflicto*, y el servidor sigue en v2 con el radio de 3 km: no se sobrescribió |
| 11, 12 | Al abrir el conflicto, la app primero pide actualizar el catálogo. Después pregunta "Resolver conflicto C2: tu captura usa v1; el servidor informa v2…", con la opción de crear una versión nueva o conservar el conflicto |
| 13 | Con "Crear nueva versión", la anterior queda *archivada* y la nueva *incorporada*. El servidor queda en v3 con el radio de 4 km |
| 14, 15, 16 | Con la sesión vencida en el núcleo, al sincronizar aparece "Sesión vencida. Inicia sesión otra vez; los registros pendientes están conservados". La evaluación (Escuela Rural El Carmen, ESCUELA) sigue *Pendiente*, y arriba se lee "nada se borró de SQLite" |
| 17 | **Hallazgo:** con la sesión vencida, "Cerrar sesión" responde "No se pudo cerrar sesión en el servidor. Reintenta con conexión", aunque sí hay conexión. Ver la nota abajo |
| 18 | Después de iniciar sesión otra vez y sincronizar, las dos evaluaciones quedan *Sincronizadas*. En el servidor la de la escuela tiene `tipoActivo=ESCUELA` |

## Hallazgos de la primera prueba

1. **Cerrar sesión con la sesión vencida (captura 17).** `SesionCampo.Salir` llamaba a `Autorizar`, que lanza `SesionVencidaException` cuando la sesión ya está marcada como vencida. **Corregido en `1a26bd3`:** con la sesión vencida, `Salir` borra la sesión local sin llamar al servidor. Verificado en la sección 3.
2. **Servidor por defecto.** La app traía `http://127.0.0.1:18085`, pero el README, el guion de la demo y la prueba instrumentada usan el túnel `adb reverse tcp:8084 tcp:18085`. **Corregido en `1a26bd3`:** el valor por defecto vuelve a ser `http://127.0.0.1:8084`. Verificado en la sección 3.
3. **Menor, sin corregir.** Los tipos de activo se ordenan por nombre, así que OTRO queda en medio de la lista. El servidor envía `orden` y se podría usar ese.

## 3. Segunda prueba, con las correcciones (`1a26bd3`)

La rama `pruebas-android` une `1a26bd3` y suma tres comprobaciones a la prueba instrumentada.

**Prueba instrumentada.** Archivo: `2026-11-17_android_instrumentado_SM-A155M_1a26bd3.log`. Resultado: **33 comprobaciones OK y RESULTADO ANDROID: APROBADO**. Las 3 nuevas:
- "401: cerrar sesión con la sesión vencida limpia la sesión local, sin error". Con el código anterior, este paso lanzaba una excepción.
- "401: después de cerrar sesión, la evaluación y la caracterización pendientes siguen en SQLite". Se consulta la base directamente, sin filtrar por la sesión.
- "401: al entrar de nuevo, la captura pendiente sigue disponible para el mismo usuario".

**Capturas, con la app normal.** El APK se compiló con los ensamblados dentro (`-p:EmbedAssembliesIntoApk=true`) para poder borrar los datos de la app y probar una instalación limpia. Con Fast Deployment, borrar los datos borra también los ensamblados y la app no arranca; eso es propio de la compilación de depuración, no de la app.

| Captura | Qué muestra |
|---|---|
| 19 | Instalación limpia (datos borrados): el servidor por defecto es `http://127.0.0.1:8084` |
| 20 | Con el túnel `adb reverse tcp:8084 tcp:18085`, se inicia sesión y el catálogo se descarga por el puerto 8084 (99 desastres) |
| 21 | Con la sesión vencida en el núcleo, se guarda una evaluación (Casa familia Rojas, VIVIENDA). Al sincronizar aparece "Sesión vencida…" y la evaluación queda *Pendiente* |
| 22 | "Cerrar sesión" con la sesión vencida funciona sin error y vuelve a "Inicia sesión antes de capturar o sincronizar". La base SQLite del celular, copiada en ese momento, conserva la evaluación pendiente |
| 23 | Al iniciar sesión otra vez y sincronizar, la evaluación queda *Sincronizada*. En el servidor tiene `tipoActivo=VIVIENDA` y `activo="Casa familia Rojas"` |
