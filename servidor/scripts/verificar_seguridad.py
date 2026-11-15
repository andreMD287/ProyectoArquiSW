"""Pruebas de seguridad del despliegue: clave de integración, ruta de migración apagada, token en
cada servicio y límite de intentos de inicio de sesión. Se corre al final de verificar.sh porque
agota a propósito el cupo de intentos de inicio de sesión de esta dirección por un minuto."""
import json
import os
import urllib.error
import urllib.request

ENTRADA = os.environ.get('MAPAS_URL', 'http://127.0.0.1:18085')
NUCLEO = os.environ.get('NUCLEO_URL', 'http://127.0.0.1:18092/plataforma-desastres/api')
total = 0


def pedir(url, metodo='GET', cuerpo=None, cabeceras=None):
    h = {'Content-Type': 'application/json', **(cabeceras or {})}
    q = urllib.request.Request(url, None if cuerpo is None else json.dumps(cuerpo).encode(), h, method=metodo)
    try:
        with urllib.request.urlopen(q, timeout=20) as r:
            return r.status
    except urllib.error.HTTPError as e:
        return e.code


def check(ok, msg, detail=None):
    global total
    if not ok:
        raise AssertionError(msg + ': ' + str(detail))
    total += 1
    print('OK: ' + msg, flush=True)


clave = os.environ.get('INTEGRACION_KEY', '')
check(pedir(NUCLEO + '/integracion/catalogo') == 401, 'el catálogo interno exige la clave de integración')
check(pedir(NUCLEO + '/integracion/catalogo', cabeceras={'X-Integracion-Key': 'clave-equivocada-123456'}) == 401,
      'una clave de integración equivocada se rechaza')
check(pedir(NUCLEO + '/integracion/catalogo', cabeceras={'X-Integracion-Key': clave[:-1]}) == 401,
      'una clave casi igual también se rechaza')
check(pedir(NUCLEO + '/integracion/catalogo', cabeceras={'X-Integracion-Key': clave}) == 200,
      'con la clave correcta el servicio interno lee el catálogo')
check(pedir(NUCLEO + '/integracion/importar', 'POST', {}, {'X-Integracion-Key': clave}) == 404,
      'la ruta de migración que actúa como el coordinador está apagada')
for ruta in ['/api/desastres', '/api/panorama', '/api/reconstruccion/desastres']:
    check(pedir(ENTRADA + ruta) == 401, 'sin token no hay acceso: ' + ruta)
check(pedir(ENTRADA + '/tablero?desastre=X') == 401, 'el tablero del portal exige sesión')

estados = [pedir(ENTRADA + '/api/auth/login', 'POST', {'usuario': 'no_existe', 'clave': 'adivinando-' + str(i)}) for i in range(45)]
check(estados.count(429) > 0 and set(estados) <= {401, 429},
      'después de muchos intentos fallidos el login responde 429', {k: estados.count(k) for k in set(estados)})
print(f'\nSeguridad: {total} comprobaciones correctas.')
