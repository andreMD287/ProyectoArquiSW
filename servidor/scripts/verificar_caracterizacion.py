"""Prueba de los contratos C2 y C1.
- C2 en el núcleo y a través de servidor (la ruta que usa la app de campo): caracterización del
  desastre (ubicación, radio, nivel y descripción), con idempotencia, versión base, permisos por rol,
  paso automático a CARACTERIZACION y la copia del catálogo en servidor con la caracterización.
- C1 en servidor: catálogo de tipos de activo y normalización (un código desconocido o ausente queda
  como OTRO, con el valor original), sin romper la idempotencia de los lotes.
Crea datos sintéticos."""
import json
import os
import time
import urllib.error
import urllib.request
import uuid

ENTRADA = os.environ.get('MAPAS_URL', 'http://127.0.0.1:18085') + '/api'
NUCLEO = os.environ.get('NUCLEO_URL', 'http://127.0.0.1:18092/plataforma-desastres/api')
total = 0


def req(base, method, path, body=None, token=None):
    h = {'Content-Type': 'application/json'}
    if token:
        h['Authorization'] = 'Bearer ' + token
    q = urllib.request.Request(base + path, None if body is None else json.dumps(body).encode(), h, method=method)
    try:
        with urllib.request.urlopen(q, timeout=30) as r:
            s, b = r.status, r.read()
    except urllib.error.HTTPError as r:
        s, b = r.code, r.read()
    try:
        b = json.loads(b)
    except ValueError:
        pass
    return s, b


def check(ok, msg, detail=None):
    global total
    if not ok:
        raise AssertionError(msg + ': ' + str(detail))
    total += 1
    print('OK: ' + msg, flush=True)


usuario = os.environ.get('PGDR_BOOTSTRAP_USER', 'coordinador_demo')
clave = os.environ.get('PGDR_BOOTSTRAP_PASSWORD', 'Arqui-Local-2026!')
s, b = req(ENTRADA, 'POST', '/auth/login', {'usuario': usuario, 'clave': clave})
check(s == 200, 'login del coordinador', (s, b))
t = b['accessToken']
tag = uuid.uuid4().hex[:8]
tokens = {}
for rol in ['EVALUADOR', 'CONSULTA']:
    nombre = f'carac_{rol.lower()}_{tag}'
    s, b = req(ENTRADA, 'POST', '/auth/usuarios', {'usuario': nombre, 'clave': 'Caracterizacion-2026!', 'rol': rol}, t)
    check(s == 201, 'crear usuario ' + rol, (s, b))
    tokens[rol] = req(ENTRADA, 'POST', '/auth/login', {'usuario': nombre, 'clave': 'Caracterizacion-2026!'})[1]['accessToken']
evaluador, consulta = tokens['EVALUADOR'], tokens['CONSULTA']

ref = 'CARAC-' + tag
d = {'id': ref, 'tipo': 'DESLIZAMIENTO', 'ubicacion': 'Vereda sintética ' + tag, 'descripcion': 'Prueba de caracterización',
     'severidad': 'MEDIA', 'fecha': '2026-11-15T12:00:00Z'}
s, b = req(ENTRADA, 'POST', '/desastres', d, t)
check(s == 201, 'desastre registrado', (s, b))

C = f'/eventos/{ref}/caracterizacion'
cuerpo = {'latitud': 4.6097, 'longitud': -74.0817, 'radioKm': 5.0, 'nivel': 3,
          'descripcion': 'Deslizamiento sobre la vía principal', 'versionBase': 0, 'claveIdempotencia': str(uuid.uuid4())}
s, b = req(NUCLEO, 'PATCH', C, cuerpo, consulta)
check(s == 403, 'CONSULTA no caracteriza', (s, b))
s, b = req(NUCLEO, 'PATCH', C, dict(cuerpo, latitud=123), evaluador)
check(s == 400, 'una latitud fuera de rango se rechaza', (s, b))
s, b = req(NUCLEO, 'PATCH', C, dict(cuerpo, nivel=5), evaluador)
check(s == 400, 'el nivel del desastre va de 1 a 4', (s, b))
s, b = req(NUCLEO, 'PATCH', '/eventos/NO-EXISTE-' + tag + '/caracterizacion', cuerpo, evaluador)
check(s == 404, 'un desastre inexistente responde 404', (s, b))

s, b = req(NUCLEO, 'PATCH', C, cuerpo, evaluador)
check(s == 200 and b['fase'] == 'CARACTERIZACION', 'el evaluador caracteriza y el desastre pasa a CARACTERIZACION', (s, b))
check(b['latitud'] == 4.6097 and b['longitud'] == -74.0817 and b['radioKm'] == 5.0, 'quedan la ubicación y el radio', b)
check(b['nivel'] == 3 and b['severidad'] == 'ALTA' and b['version'] == 1, 'el nivel 3 queda como ALTA y la versión sube a 1', b)

s2, b2 = req(NUCLEO, 'PATCH', C, cuerpo, evaluador)
check(s2 == 200 and b2 == b, 'repetir la misma clave devuelve el mismo resultado (idempotencia)', (s2, b2))
s, b = req(NUCLEO, 'PATCH', C, dict(cuerpo, radioKm=9.0), evaluador)
check(s == 409 and b.get('error') == 'CLAVE_REUTILIZADA', 'la misma clave con otro contenido responde 409', (s, b))
s, b = req(NUCLEO, 'PATCH', C, dict(cuerpo, radioKm=9.0, claveIdempotencia=str(uuid.uuid4())), evaluador)
check(s == 409 and b.get('error') == 'VERSION_DESACTUALIZADA' and b.get('versionActual') == 1,
      'una versión base vieja responde 409 con la versión actual', (s, b))
s, b = req(NUCLEO, 'PATCH', C, dict(cuerpo, radioKm=9.0, nivel=None, versionBase=1, claveIdempotencia=str(uuid.uuid4())), t)
check(s == 200 and b['radioKm'] == 9.0 and b['fase'] == 'CARACTERIZACION' and b['version'] == 2,
      'una segunda caracterización actualiza el radio sin cambiar la fase', (s, b))

s, b = req(ENTRADA, 'GET', f'/desastres/{ref}/historial', token=t)
check(s == 200 and len(b) == 2 and all('Caracterización' in x['motivo'] for x in b), 'el historial guarda las dos caracterizaciones', (s, b))

for _ in range(40):
    s, b = req(ENTRADA, 'GET', '/desastres', token=t)
    propio = [x['desastre'] for x in b.get('desastres', []) if x['desastre']['id'] == ref] if s == 200 else []
    if propio and propio[0].get('version') == 2:
        break
    time.sleep(0.5)
check(propio and propio[0].get('fase') == 'CARACTERIZACION' and propio[0].get('version') == 2,
      'la copia de servidor recibe la fase y la versión nuevas', propio)
check(propio[0].get('latitud') == 4.6097 and propio[0].get('radioKm') == 9.0 and propio[0].get('nivel') == 3,
      'la copia de servidor conserva la ubicación, el radio y el nivel (C2)', propio[0])

# --- C2 a través de servidor: lo que usa la app de campo ---
ref2 = 'CARAC2-' + tag
s, b = req(ENTRADA, 'POST', '/desastres', dict(d, id=ref2), t)
check(s == 201, 'segundo desastre registrado', (s, b))
S = f'/desastres/{ref2}/caracterizacion'
cuerpo2 = {'latitud': 4.71, 'longitud': -74.07, 'radioKm': 2.5, 'nivel': 2, 'descripcion': 'Caracterizado desde la app',
           'versionBase': 0, 'claveIdempotencia': str(uuid.uuid4())}
s, b = req(ENTRADA, 'PATCH', S, cuerpo2)
check(s == 401, 'servidor exige sesión para caracterizar', s)
s, b = req(ENTRADA, 'PATCH', S, cuerpo2, consulta)
check(s == 403, 'servidor: CONSULTA no caracteriza', (s, b))
s, b = req(ENTRADA, 'PATCH', S, cuerpo2, evaluador)
check(s == 200 and b.get('fase') == 'CARACTERIZACION' and b.get('version') == 1 and b.get('radioKm') == 2.5,
      'servidor reenvía la caracterización del evaluador al núcleo (C2)', (s, b))
s2, b2 = req(ENTRADA, 'PATCH', S, cuerpo2, evaluador)
check(s2 == 200 and b2 == b, 'servidor: repetir la misma clave devuelve el mismo resultado', (s2, b2))
s, b = req(ENTRADA, 'PATCH', S, dict(cuerpo2, radioKm=4.0, claveIdempotencia=str(uuid.uuid4())), evaluador)
check(s == 409 and b.get('error') == 'VERSION_DESACTUALIZADA' and b.get('versionActual') == 1,
      'servidor no convierte el conflicto en éxito: 409 con la versión actual', (s, b))
s, b = req(ENTRADA, 'PATCH', f'/desastres/NO-EXISTE-{tag}/caracterizacion', cuerpo2, evaluador)
check(s == 404, 'servidor: un desastre inexistente responde 404', (s, b))

# --- C1: tipos de activo físico ---
s, b = req(ENTRADA, 'GET', '/catalogos', token=evaluador)
codigos = [x['codigo'] for x in b.get('tiposActivo', [])] if s == 200 else []
check(s == 200 and {'VIVIENDA', 'ESCUELA', 'PUENTE', 'VIA', 'OTRO'} <= set(codigos) and len(codigos) >= 9,
      'el catálogo trae los tipos de activo (C1)', (s, codigos))
for _ in range(40):
    s, b = req(ENTRADA, 'GET', '/desastres', token=evaluador)
    if s == 200 and any(x['desastre']['id'] == ref2 for x in b.get('desastres', [])):
        break
    time.sleep(0.5)
ops = []
for clave_op, tipo in [('puente', 'PUENTE'), ('torre', 'TORRE_ANTENA'), ('sin', None)]:
    op = {'operacionId': str(uuid.uuid4()), 'evaluacionId': str(uuid.uuid4()), 'eventoId': ref2, 'nivelAfectacion': 4,
          'latitud': 4.71, 'longitud': -74.07, 'capturadaEnUtc': '2026-11-17T12:00:00Z', 'descripcion': 'C1 ' + clave_op,
          'activo': 'Activo ' + clave_op}
    if tipo:
        op['tipoActivo'] = tipo
    ops.append(op)
s, b = req(ENTRADA, 'POST', '/campo/lotes', {'operaciones': ops}, evaluador)
check(s in (200, 202) and all(r['estado'] in ('recibida', 'incorporada') for r in b['resultados']),
      'un lote con un código válido, uno desconocido y uno sin código se recibe completo', (s, b))
s, b = req(ENTRADA, 'POST', '/campo/lotes', {'operaciones': ops}, evaluador)
check(s in (200, 202) and all(r['estado'] in ('recibida', 'incorporada') for r in b['resultados']),
      'reenviar el mismo lote no crea conflicto (idempotencia intacta)', b)
for _ in range(20):
    s, b = req(ENTRADA, 'GET', '/panorama?clase=EVALUACION&eventoId=' + ref2, token=evaluador)
    if s == 200 and len(b) == 3:
        break
    time.sleep(0.5)
por_desc = {x['descripcion']: x for x in b}
check(por_desc['C1 puente']['tipoActivo'] == 'PUENTE' and por_desc['C1 puente']['tipoActivoNombre'] == 'Puente'
      and por_desc['C1 puente'].get('tipoActivoOriginal') is None, 'un código válido se conserva', por_desc['C1 puente'])
check(por_desc['C1 torre']['tipoActivo'] == 'OTRO' and por_desc['C1 torre']['tipoActivoOriginal'] == 'TORRE_ANTENA',
      'un código desconocido queda como OTRO y se conserva el original', por_desc['C1 torre'])
check(por_desc['C1 sin']['tipoActivo'] == 'OTRO' and por_desc['C1 sin'].get('tipoActivoOriginal') is None,
      'una evaluación sin código (app anterior) queda como OTRO', por_desc['C1 sin'])
s, b = req(ENTRADA, 'GET', '/campo/evaluaciones/' + ops[1]['evaluacionId'], token=evaluador)
check(s == 200 and b['tipoActivo'] == 'OTRO' and b['tipoActivoOriginal'] == 'TORRE_ANTENA' and b['activo'] == 'Activo torre',
      'el detalle de la evaluación trae el tipo normalizado, el original y el texto libre', b)
print(f'\nCaracterización y tipos de activo (C2 y C1): {total} comprobaciones correctas.')
