"""Mide el tablero del portal (servlet asíncrono multihilo, ADR-24) en el ambiente entregado.

Para cada escenario envía N solicitudes y reporta p50, p95, máximo, solicitudes por segundo y
respuestas no exitosas:
  1. paralelo y secuencial con 1 cliente: latencia de una consulta sin carga.
  2. paralelo y secuencial con C clientes concurrentes: comportamiento bajo carga en una máquina.
  3. sin-reconstruccion: Reconstrucción congelada con `docker pause`; el tablero debe responder
     con las otras dos fuentes al vencer el límite de esa fuente (degradación parcial).

Uso: python portal/scripts/medir_tablero.py [--n 300] [--c 20] [--salida evidencias/rol1-tablero]
Requiere la plataforma levantada (./iniciar.sh). Crea un desastre sintético con evaluaciones.
"""
import argparse
import concurrent.futures
import http.cookiejar
import json
import os
import platform
import statistics
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path

BASE = os.environ.get('MAPAS_URL', 'http://127.0.0.1:18085')
COMPOSE = ['docker', 'compose', '-f', str(Path(__file__).resolve().parents[2] / 'servidor' / 'compose.yaml')]


def api(metodo, ruta, cuerpo=None, token=None):
    h = {'Content-Type': 'application/json'}
    if token:
        h['Authorization'] = 'Bearer ' + token
    q = urllib.request.Request(BASE + '/api' + ruta, None if cuerpo is None else json.dumps(cuerpo).encode(), h, method=metodo)
    with urllib.request.urlopen(q, timeout=30) as r:
        return json.loads(r.read() or b'null')


def sesion_portal(usuario, clave):
    cookies = http.cookiejar.CookieJar()
    abrir = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookies))
    abrir.open(urllib.request.Request(BASE + '/login', urllib.parse.urlencode({'usuario': usuario, 'clave': clave}).encode(),
                                      method='POST'), timeout=30).read()
    jsession = next(c.value for c in cookies if c.name == 'JSESSIONID')
    return 'JSESSIONID=' + jsession


def preparar(usuario, clave):
    """Desastre caracterizado, con evaluaciones y en Reconstrucción, para que las tres fuentes respondan con datos."""
    t = api('POST', '/auth/login', {'usuario': usuario, 'clave': clave})['accessToken']
    ref = 'MEDICION-' + uuid.uuid4().hex[:8]
    api('POST', '/desastres', {'id': ref, 'tipo': 'INUNDACION', 'ubicacion': 'Zona de medición', 'descripcion': 'Medición del tablero',
                               'severidad': 'ALTA', 'fecha': datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')}, t)
    for _ in range(40):
        if any(x['desastre']['id'] == ref for x in api('GET', '/desastres', token=t)['desastres']):
            break
        time.sleep(0.5)
    ops = [{'operacionId': str(uuid.uuid4()), 'evaluacionId': str(uuid.uuid4()), 'eventoId': ref, 'nivelAfectacion': 1 + i % 5,
            'latitud': 4.6 + i / 1000, 'longitud': -74.1 + i / 1000, 'capturadaEnUtc': '2026-11-15T12:00:00Z',
            'descripcion': f'Evaluación {i}', 'activo': ['Vivienda', 'Vía', 'Puente'][i % 3]} for i in range(30)]
    for i in range(0, len(ops), 20):   # servidor acepta lotes de 1 a 20 operaciones
        api('POST', '/campo/lotes', {'operaciones': ops[i:i + 20]}, t)
    return ref


def un_pedido(cookie, ref, modo):
    q = urllib.request.Request(f'{BASE}/tablero?desastre={ref}' + ('&modo=secuencial' if modo == 'secuencial' else ''),
                               headers={'Cookie': cookie})
    inicio = time.perf_counter()
    try:
        with urllib.request.urlopen(q, timeout=30) as r:
            cuerpo = json.loads(r.read())
            estado = r.status
    except urllib.error.HTTPError as e:
        estado, cuerpo = e.code, {}
    return (time.perf_counter() - inicio) * 1000, estado, cuerpo.get('fuentesNoDisponibles', [])


def percentil(valores, p):
    ordenados = sorted(valores)
    k = max(0, min(len(ordenados) - 1, int(round(p / 100 * len(ordenados) + 0.5)) - 1))
    return ordenados[k]


def escenario(nombre, cookie, ref, modo, n, c):
    for _ in range(10):   # calentamiento
        un_pedido(cookie, ref, modo)
    inicio = time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=c) as pool:
        resultados = list(pool.map(lambda _: un_pedido(cookie, ref, modo), range(n)))
    duracion = time.perf_counter() - inicio
    ms = [r[0] for r in resultados if r[1] == 200]
    parciales = sum(1 for r in resultados if r[1] == 200 and r[2])
    res = {'escenario': nombre, 'solicitudes': n, 'concurrencia': c, 'exitosas': len(ms),
           'no_exitosas': {str(k): sum(1 for r in resultados if r[1] == k) for k in sorted({r[1] for r in resultados if r[1] != 200})},
           'respuestas_parciales': parciales,
           'p50_ms': round(percentil(ms, 50), 1) if ms else None, 'p95_ms': round(percentil(ms, 95), 1) if ms else None,
           'max_ms': round(max(ms), 1) if ms else None, 'media_ms': round(statistics.mean(ms), 1) if ms else None,
           'solicitudes_por_segundo': round(n / duracion, 1)}
    print(json.dumps(res, ensure_ascii=False), flush=True)
    return res


def main():
    a = argparse.ArgumentParser()
    a.add_argument('--n', type=int, default=300)
    a.add_argument('--c', type=int, default=20)
    a.add_argument('--salida', default='evidencias/rol1-tablero')
    args = a.parse_args()
    usuario = os.environ.get('PGDR_BOOTSTRAP_USER', 'coordinador_demo')
    clave = os.environ.get('PGDR_BOOTSTRAP_PASSWORD', 'Arqui-Local-2026!')
    ref = preparar(usuario, clave)
    cookie = sesion_portal(usuario, clave)
    # Un solo cliente: muestra la latencia de una consulta (paralelo ~ la fuente más lenta; secuencial ~ la suma).
    # Con C clientes: muestra el comportamiento bajo carga en una sola máquina.
    resultados = [escenario('paralelo-1-cliente', cookie, ref, 'paralelo', max(50, args.n // 3), 1),
                  escenario('secuencial-1-cliente', cookie, ref, 'secuencial', max(50, args.n // 3), 1),
                  escenario('paralelo', cookie, ref, 'paralelo', args.n, args.c),
                  escenario('secuencial', cookie, ref, 'secuencial', args.n, args.c)]
    subprocess.run(COMPOSE + ['pause', 'reconstruccion'], check=True, capture_output=True)
    try:
        resultados.append(escenario('sin-reconstruccion', cookie, ref, 'paralelo', max(40, args.n // 5), args.c))
    finally:
        subprocess.run(COMPOSE + ['unpause', 'reconstruccion'], check=True, capture_output=True)
    informe = {'fecha': datetime.now(timezone.utc).isoformat(timespec='seconds'), 'ambiente': 'Docker Compose en una máquina',
               'equipo': platform.platform(), 'desastre': ref,
               'limites': 'pool del tablero 16 hilos y cola 64; límite por fuente 1500 ms; límite total 3000 ms',
               'resultados': resultados}
    salida = Path(args.salida)
    salida.mkdir(parents=True, exist_ok=True)
    archivo = salida / f"medicion_tablero_{datetime.now().strftime('%Y-%m-%d_%H%M')}.json"
    archivo.write_text(json.dumps(informe, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Informe: ' + str(archivo))


if __name__ == '__main__':
    main()
