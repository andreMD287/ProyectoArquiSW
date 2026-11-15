"""Prueba del contrato C2 en el núcleo: caracterización del desastre (ubicación, radio, nivel y
descripción), con idempotencia, versión base, permisos por rol y paso automático a CARACTERIZACION.
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
print(f'\nCaracterización (C2): {total} comprobaciones correctas.')
