"""Prueba de punta a punta del portal MVC (UD-09) y su servlet multihilo, navegando como un
usuario: inicio de sesión, desastres, caracterización, tablero, evaluaciones, Reconstrucción
física con aprobación a dos manos, mapa y cierre de sesión. Crea datos sintéticos."""
import html
import http.cookiejar
import json
import os
import re
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE = os.environ.get('MAPAS_URL', 'http://127.0.0.1:18085')
total = 0


def check(ok, msg, detail=None):
    global total
    if not ok:
        raise AssertionError(msg + ': ' + str(detail)[:600])
    total += 1
    print('OK: ' + msg, flush=True)


class SinRedireccion(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


class Navegador:
    """Un navegador con su propia cookie de sesión. No sigue redirecciones solo: las sigue seguir()."""

    def __init__(self):
        self.cookies = http.cookiejar.CookieJar()
        self.abrir = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cookies), SinRedireccion()).open

    def pedir(self, metodo, ruta, datos=None):
        cuerpo = None if datos is None else urllib.parse.urlencode(datos, doseq=True).encode()
        q = urllib.request.Request(BASE + ruta, cuerpo, {'Content-Type': 'application/x-www-form-urlencoded'}, method=metodo)
        try:
            with self.abrir(q, timeout=30) as r:
                return r.status, r.headers, r.read().decode('utf-8', 'replace')
        except urllib.error.HTTPError as r:
            return r.code, r.headers, r.read().decode('utf-8', 'replace')

    def seguir(self, metodo, ruta, datos=None):
        s, h, b = self.pedir(metodo, ruta, datos)
        saltos = 0
        while s in (301, 302, 303) and saltos < 5:
            destino = urllib.parse.urlparse(h['Location'])
            s, h, b = self.pedir('GET', destino.path + ('?' + destino.query if destino.query else ''))
            saltos += 1
        return s, h, b

    def entrar(self, usuario, clave):
        s, h, _ = self.pedir('POST', '/login', {'usuario': usuario, 'clave': clave})
        check(s == 302 and h['Location'].endswith('/desastres'), 'inicia sesión en el portal: ' + usuario, (s, dict(h)))


def avisos(pagina):
    return re.findall(r'class="aviso[^"]*"[^>]*>([^<]*)', pagina)


def api(metodo, ruta, cuerpo=None, token=None):
    h = {'Content-Type': 'application/json'}
    if token:
        h['Authorization'] = 'Bearer ' + token
    q = urllib.request.Request(BASE + '/api' + ruta, None if cuerpo is None else json.dumps(cuerpo).encode(), h, method=metodo)
    try:
        with urllib.request.urlopen(q, timeout=30) as r:
            return r.status, json.loads(r.read() or b'null')
    except urllib.error.HTTPError as r:
        return r.code, r.read()


usuario = os.environ.get('PGDR_BOOTSTRAP_USER', 'coordinador_demo')
clave = os.environ.get('PGDR_BOOTSTRAP_PASSWORD', 'Arqui-Local-2026!')
tag = uuid.uuid4().hex[:8]
CLAVE = 'Portal-Prueba-2026!'

# --- Sesión ---
coord = Navegador()
s, h, b = coord.pedir('GET', '/desastres')
check(s == 302 and '/login' in h['Location'], 'sin sesión el portal lleva al inicio de sesión', (s, dict(h)))
s, _, b = coord.pedir('POST', '/login', {'usuario': usuario, 'clave': 'clave-equivocada'})
check(s == 200 and 'incorrectos' in b, 'una contraseña equivocada no entra', s)
coord.entrar(usuario, clave)
check(any(c.name == 'JSESSIONID' for c in coord.cookies), 'la sesión vive en una cookie del portal (el token no va al navegador)')
s, _, b = coord.seguir('GET', '/desastres')
check(s == 200 and 'Desastres del organismo' in b and 'Registrar desastre' in b, 'el coordinador ve la lista y puede registrar', s)

# --- Usuarios del organismo (desde el portal) ---
for rol in ['EVALUADOR', 'CONSULTA', 'COORDINADOR']:
    nombre = f'portal_{rol.lower()}_{tag}'
    s, _, b = coord.seguir('POST', '/usuarios', {'usuario': nombre, 'clave': CLAVE, 'rol': rol})
    check(s == 200 and 'creado' in b, 'el coordinador crea un usuario ' + rol, avisos(b))
evaluador, consulta, coord2 = Navegador(), Navegador(), Navegador()
evaluador.entrar(f'portal_evaluador_{tag}', CLAVE)
consulta.entrar(f'portal_consulta_{tag}', CLAVE)
coord2.entrar(f'portal_coordinador_{tag}', CLAVE)
s, _, b = consulta.seguir('GET', '/desastres')
check(s == 200 and 'Registrar desastre' not in b, 'CONSULTA no ve la acción de registrar', s)
s, _, b = consulta.seguir('GET', '/usuarios')
check(s == 403, 'CONSULTA no administra usuarios', s)

# --- Registro y caracterización del desastre ---
s, h, _ = coord.pedir('POST', '/desastres', {'tipo': 'DESLIZAMIENTO', 'ubicacion': 'Vereda portal ' + tag,
                                              'descripcion': 'Prueba del portal', 'severidad': 'MEDIA'})
check(s == 302 and '/desastres/DES-' in h['Location'], 'el coordinador registra un desastre', (s, dict(h)))
ref = h['Location'].rsplit('/', 1)[1]
s, _, b = coord.seguir('GET', '/desastres/' + ref)
check(s == 200 and 'Registro' in b and 'Caracterizar el desastre' in b, 'el tablero del desastre abre en fase Registro', s)
version = re.search(r'name="versionBase" value="(\d+)"', b).group(1)
s, _, b = evaluador.seguir('POST', f'/desastres/{ref}/caracterizacion', {'versionBase': version, 'latitud': '4.6097',
                                                                         'longitud': '-74.0817', 'radioKm': '3', 'nivel': '3', 'descripcion': 'Desde el portal'})
check(s == 200 and 'Caracterización guardada' in b and 'fase-caracterizacion' in b,
      'el evaluador caracteriza y el desastre pasa a Caracterización', avisos(b))
s, _, b = coord.seguir('POST', f'/desastres/{ref}/caracterizacion', {'versionBase': version, 'latitud': '4.6',
                                                                     'longitud': '-74.08', 'radioKm': '5'})
check(s == 200 and 'aviso error' in b, 'una versión vieja se rechaza sin sobrescribir', avisos(b))

# --- Evaluaciones capturadas por el evaluador (como lo haría la app de campo) ---
token_eval = api('POST', '/auth/login', {'usuario': f'portal_evaluador_{tag}', 'clave': CLAVE})[1]['accessToken']
# Como la app de campo: primero descarga el catálogo de servidor (copia de lectura del núcleo, ADR-03).
import time
for _ in range(40):
    s, cat = api('GET', '/desastres', token=token_eval)
    if s == 200 and any(x['desastre']['id'] == ref for x in cat['desastres']):
        break
    time.sleep(0.5)
check(any(x['desastre']['id'] == ref for x in cat['desastres']), 'el desastre llega a la copia de servidor que descarga la app')
evaluaciones = {}
for nombre, nivel, lat, lon in [('Puente', 4, 4.611, -74.082), ('Escuela', 5, 4.605, -74.079)]:
    op = {'operacionId': str(uuid.uuid4()), 'evaluacionId': str(uuid.uuid4()), 'eventoId': ref, 'nivelAfectacion': nivel,
          'latitud': lat, 'longitud': lon, 'capturadaEnUtc': '2026-11-15T12:05:00Z', 'descripcion': 'Daño en ' + nombre, 'activo': nombre}
    s, b2 = api('POST', '/campo/lotes', {'operaciones': [op]}, token_eval)
    check(s in (200, 202), 'evaluación de ' + nombre + ' capturada', (s, b2))
    evaluaciones[nombre] = op['evaluacionId']
s, _, b = coord.seguir('GET', '/evaluaciones?desastre=' + ref)
check(s == 200 and b.count('Daño en') == 0 and 'Puente' in b and 'Escuela' in b, 'la lista de evaluaciones del desastre las muestra', s)
s, _, b = coord.seguir('GET', '/evaluaciones/' + evaluaciones['Puente'])
check(s == 200 and 'Historial de versiones' in b and 'Puente' in b, 'el detalle de la evaluación muestra su historial', s)

# --- Tablero multihilo ---
s, _, b = coord.seguir('GET', '/tablero?desastre=' + ref)
t = json.loads(b)
check(s == 200 and t['modo'] == 'paralelo' and t['fuentesNoDisponibles'] == [], 'el tablero responde con las tres fuentes', t.get('fuentes'))
hilos = {f['hilo'] for f in t['fuentes'].values()}
check(all(x.startswith('tablero-') for x in hilos), 'las fuentes corren en hilos del pool del tablero', hilos)
check(t['desastre']['fase'] == 'CARACTERIZACION' and t['desastre']['radioKm'] == 3.0, 'el tablero trae fase y área del desastre', t['desastre'])
check(t['evaluaciones']['total'] == 2 and t['evaluaciones']['porNivel'] == {'4': 1, '5': 1}, 'el tablero resume las evaluaciones por nivel', t['evaluaciones'])
s, _, b = coord.seguir('GET', f'/tablero?desastre={ref}&modo=secuencial')
check(s == 200 and json.loads(b)['modo'] == 'secuencial', 'el tablero también corre en modo secuencial (para medir)', s)
s, _, b = Navegador().pedir('GET', '/tablero?desastre=' + ref)
check(s == 401, 'el tablero exige sesión', s)
s, _, b = coord.seguir('GET', '/tablero?desastre=no%20valido')
check(s == 400, 'el tablero valida la referencia', s)

# --- Reconstrucción física desde el portal (gRPC) ---
s, _, b = coord.seguir('POST', f'/reconstruccion/{ref}/iniciar')
check(s == 200 and 'pasó a Reconstrucción' in b, 'el coordinador inicia la reconstrucción', avisos(b))
check(evaluaciones['Puente'] in b and 'Crear proyecto' in b, 'el portal lista las evaluaciones disponibles para crear proyectos', s)
s, _, b = coord.seguir('POST', f'/reconstruccion/{ref}/proyectos', {'nombre': 'Puente ' + tag, 'evaluaciones': [evaluaciones['Puente']]})
check(s == 200 and 'Proyecto creado' in b, 'el coordinador crea un proyecto desde la evaluación', avisos(b))
pid = re.search(r'/reconstruccion/' + re.escape(ref) + r'/proyectos/([0-9a-f-]{36})', b).group(1)
P = f'/reconstruccion/{ref}/proyectos/{pid}'
aprobacion = {'presupuesto': '1000000', 'fuente': 'Fondo de reconstrucción', 'responsable': f'portal_evaluador_{tag}'}
s, _, b = coord.seguir('POST', P + '/aprobar', aprobacion)
check(s == 200 and 'RN-19' in b, 'quien formuló el proyecto no lo aprueba (RN-19)', avisos(b))
s, _, b = coord2.seguir('POST', P + '/aprobar', aprobacion)
check(s == 200 and 'Proyecto aprobado' in b and 'APROBADO' in b, 'un segundo coordinador aprueba el proyecto', avisos(b))
pasos = [('iniciar', {'contratista': 'Consorcio Vial', 'inicio': '2026-12-01', 'fin': '2027-03-31'}, 'Ejecución iniciada'),
         ('avance', {'porcentaje': '100', 'descripcion': 'Obra terminada'}, 'Avance registrado'),
         ('evidencia', {'descripcion': 'Acta de entrega 1'}, 'Evidencia adjunta'),
         ('estado', {'estado': 'FINALIZADO'}, 'FINALIZADO')]
for accion, datos, esperado in pasos:
    s, _, b = evaluador.seguir('POST', P + '/' + accion, datos)
    check(s == 200 and esperado in b, 'el responsable asignado: ' + accion, avisos(b))
s, _, b = coord.seguir('GET', P)
check(s == 200 and 'Auditoría' in b and 'RECHAZADO' in b, 'la auditoría muestra el intento rechazado y los cambios', s)
s, _, b = coord.seguir('GET', f'/tablero?desastre={ref}')
r = json.loads(b)['reconstruccion']
check(r['avancePonderado'] == 100 and r['proyectos'][0]['estado'] == 'FINALIZADO', 'el tablero refleja el proyecto finalizado', r)

# --- Mapa y salida ---
s, _, b = coord.seguir('GET', '/mapa')
check(s == 200 and ref in b and 'mapa-general.js' in b, 'el mapa general incluye el desastre caracterizado', s)
s, h, _ = coord.pedir('POST', '/logout')
check(s == 302 and 'salida' in h['Location'], 'cerrar sesión', (s, dict(h)))
s, h, _ = coord.pedir('GET', '/desastres')
check(s == 302 and '/login' in h['Location'], 'después de salir el portal pide sesión otra vez', s)
print(f'\nPortal MVC: {total} comprobaciones correctas.')
