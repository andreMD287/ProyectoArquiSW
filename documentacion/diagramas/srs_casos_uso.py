"""Genera srs_casos_uso.svg: diagrama de casos de uso UML de los mínimos a–d.

Se dibuja con posiciones fijas para que ninguna asociación cruce otra. Los permisos salen del SAD §3.1
y de los casos de uso de Reconstrucción (AUTORIDAD = COORDINADOR, RESPONSABLE = EVALUADOR,
AUDITOR = CONSULTA). Solo aparecen funciones construidas.
Uso: python srs_casos_uso.py  (escribe srs_casos_uso.svg junto a este archivo)
"""
import math
from pathlib import Path

W, H = 1790, 1460
AZUL, BORDE, TEXTO, GRIS = "#1E3A8A", "#374151", "#111827", "#6B7280"
FUENTE = "Segoe UI, Arial, sans-serif"
partes = []


def t(x, y, texto, size=15, weight="normal", anchor="middle", style="normal", color=TEXTO):
    texto = texto.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    partes.append(f'<text x="{x}" y="{y}" font-family="{FUENTE}" font-size="{size}" font-weight="{weight}" '
                  f'font-style="{style}" fill="{color}" text-anchor="{anchor}">{texto}</text>')


def caja(x1, y1, x2, y2, titulo, size=16, borde=BORDE, ancho=1.6):
    partes.append(f'<rect x="{x1}" y="{y1}" width="{x2 - x1}" height="{y2 - y1}" rx="6" fill="none" '
                  f'stroke="{borde}" stroke-width="{ancho}"/>')
    t(x1 + 14, y1 + 24, titulo, size=size, weight="bold", anchor="start")


UC = {}


def uc(clave, cx, cy, rx, ry, *lineas):
    UC[clave] = (cx, cy, rx, ry)
    partes.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="#EFF6FF" stroke="{AZUL}" stroke-width="1.4"/>')
    alto = 18
    y0 = cy - (len(lineas) - 1) * alto / 2 + 5
    for i, linea in enumerate(lineas):
        t(cx, y0 + i * alto, linea, size=14)


def borde_uc(clave, hacia):
    """Punto del borde de la elipse en dirección a 'hacia'."""
    cx, cy, rx, ry = UC[clave]
    dx, dy = hacia[0] - cx, hacia[1] - cy
    k = 1 / math.sqrt((dx / rx) ** 2 + (dy / ry) ** 2)
    return (round(cx + dx * k, 1), round(cy + dy * k, 1))


def izq(clave):
    cx, cy, rx, ry = UC[clave]
    return (cx - rx, cy)


def der(clave):
    cx, cy, rx, ry = UC[clave]
    return (cx + rx, cy)


ACT = {}


def actor(clave, x, y, nombre, abstracto=False):
    ACT[clave] = (x, y)
    s = f'stroke="{AZUL}" stroke-width="2" fill="none"'
    partes.append(f'<circle cx="{x}" cy="{y + 13}" r="13" fill="#DBEAFE" stroke="{AZUL}" stroke-width="2"/>')
    partes.append(f'<line x1="{x}" y1="{y + 26}" x2="{x}" y2="{y + 62}" {s}/>')
    partes.append(f'<line x1="{x - 22}" y1="{y + 40}" x2="{x + 22}" y2="{y + 40}" {s}/>')
    partes.append(f'<line x1="{x}" y1="{y + 62}" x2="{x - 17}" y2="{y + 90}" {s}/>')
    partes.append(f'<line x1="{x}" y1="{y + 62}" x2="{x + 17}" y2="{y + 90}" {s}/>')
    lineas = nombre.split("\n")
    for i, linea in enumerate(lineas):
        t(x, y + 112 + i * 18, linea, size=15, weight="bold", style="italic" if abstracto else "normal")


def linea(puntos, punteada=False, flecha=None):
    d = " ".join(f"{x},{y}" for x, y in puntos)
    extra = ' stroke-dasharray="7 5"' if punteada else ""
    marca = f' marker-end="url(#{flecha})"' if flecha else ""
    partes.append(f'<polyline points="{d}" fill="none" stroke="{BORDE}" stroke-width="1.5"{extra}{marca}/>')


def asociacion(desde, clave_uc, via=None):
    via = via or []
    destino = via[-1] if via else desde
    linea([desde] + via + [borde_uc(clave_uc, destino)])


def dependencia(origen, destino, etiqueta, lx, ly, anchor="middle", extra=None):
    a = borde_uc(origen, UC[destino][:2])
    b = borde_uc(destino, UC[origen][:2])
    linea([a, b], punteada=True, flecha="abierta")
    t(lx, ly, etiqueta, size=13, anchor=anchor)
    if extra:
        t(lx, ly + 16, extra, size=13, anchor=anchor)


partes.append(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">')
partes.append('<defs>'
              f'<marker id="abierta" viewBox="0 0 12 12" refX="11" refY="6" markerWidth="12" markerHeight="12" orient="auto">'
              f'<path d="M1,1 L11,6 L1,11" fill="none" stroke="{BORDE}" stroke-width="1.5"/></marker>'
              f'<marker id="triangulo" viewBox="0 0 16 16" refX="15" refY="8" markerWidth="16" markerHeight="16" orient="auto">'
              f'<path d="M1,1 L15,8 L1,15 Z" fill="#FFFFFF" stroke="{BORDE}" stroke-width="1.5"/></marker>'
              '</defs>')
partes.append(f'<rect width="{W}" height="{H}" fill="#FFFFFF"/>')
t(W / 2, 34, "Casos de uso de los mínimos del proyecto (a–d)", size=20, weight="bold")

# Límite del sistema y paquetes por mínimo
partes.append(f'<rect x="250" y="62" width="1320" height="1240" fill="none" stroke="{AZUL}" stroke-width="2.2"/>')
t(910, 88, "Plataforma para la Gestión de Desastres y Reconstrucción", size=18, weight="bold")
caja(280, 104, 1540, 322, "a. App móvil que recolecta y caracteriza")
caja(280, 392, 880, 978, "b. Reconstrucción física")
caja(930, 392, 1540, 792, "d. Evaluación de daños")
caja(280, 1084, 1540, 1286, "c. Mapa con desastres, evaluaciones y proyectos")

# Casos de uso
uc("CAR", 560, 214, 155, 34, "Caracterizar el desastre", "(también sin conexión)")
uc("SYN", 1300, 172, 120, 24, "Sincronizar capturas")
uc("CON", 1300, 272, 118, 30, "Resolver conflicto", "de versión")
uc("REG", 420, 354, 112, 22, "Registrar desastre")

uc("AVA", 580, 482, 195, 44, "Registrar avance, costo", "y evidencia", "[RN-21: solo el responsable asignado]")
uc("INI", 580, 580, 150, 32, "Iniciar reconstrucción", "[RN-01, RN-02]")
uc("FOR", 580, 664, 150, 32, "Formular proyectos", "desde evaluaciones")
uc("APR", 580, 748, 180, 32, "Aprobar proyecto", "[RN-20: no quien lo formuló]")
uc("CER", 580, 832, 150, 32, "Cerrar el desastre", "[RN-04]")
uc("AUD", 580, 920, 150, 32, "Consultar seguimiento", "y auditoría")

uc("EVD", 1235, 472, 175, 34, "Registrar evaluación de daños", "(también sin conexión)")
uc("FOT", 1060, 604, 100, 24, "Adjuntar fotografía")
uc("TIP", 1405, 604, 115, 24, "Elegir tipo de activo")
uc("CEV", 1100, 736, 125, 24, "Consultar evaluaciones")

uc("MAP", 432, 1242, 132, 24, "Consultar mapa general")
uc("TAB", 724, 1242, 140, 30, "Consultar tablero", "del desastre")
uc("PAN", 578, 1150, 112, 24, "Visualizar panorama")

# Actores
actor("COO", 110, 400, "COORDINADOR")
actor("EVA", 1700, 318, "EVALUADOR")
actor("CNS", 110, 880, "CONSULTA")
actor("USU", 910, 1322, "Usuario del organismo", abstracto=True)

# Asociaciones del COORDINADOR (todas hacia su izquierda; ninguna cruza)
coo = (136, 444)
for clave in ("REG", "CAR", "INI", "FOR", "APR", "CER", "AUD"):
    linea([coo, izq(clave)])
# CONSULTA
linea([(136, 924), izq("AUD")])
# EVALUADOR: directas a d y a la sincronización; por los pasillos entre paquetes a Caracterizar y a Avance
eva = (1690, 398)
linea([(1688, 326), der("SYN")])
linea([eva, der("EVD")])
linea([(1678, 356), (652, 356), borde_uc("CAR", (652, 300))])
linea([(1678, 372), (742, 372), borde_uc("AVA", (742, 300))])
# Usuario del organismo: consultas comunes a los tres roles
linea([(890, 1342), (520, 1300), borde_uc("MAP", (520, 1300))])
linea([(892, 1330), der("TAB")])
linea([(910, 1320), (910, 736), borde_uc("CEV", (900, 736))])

# include / extend
dependencia("CON", "SYN", "«extend»", 1288, 214, anchor="end", extra="[respuesta 409]")
dependencia("EVD", "TIP", "«include»", 1356, 548, anchor="start")
dependencia("FOT", "EVD", "«extend»", 1124, 548, anchor="end")
dependencia("MAP", "PAN", "«include»", 470, 1196, anchor="end")
dependencia("TAB", "PAN", "«include»", 688, 1196, anchor="start")

# Generalización: los tres roles son Usuario del organismo (triángulo hueco hacia el padre)
linea([(86, 444), (52, 444), (52, 1368), (110, 1368)])
linea([(110, 1000), (110, 1368), (884, 1368)], flecha="triangulo")
linea([(1700, 442), (1700, 1368), (936, 1368)], flecha="triangulo")

# Nota
nx, ny = 990, 1112
partes.append(f'<path d="M{nx},{ny} h500 v152 h-500 Z" fill="#FFFBEB" stroke="{GRIS}" stroke-width="1.2"/>')
for i, texto in enumerate([
    "Entre corchetes: reglas de negocio del SAD (RN-01 a RN-21).",
    "Solo funciones construidas. La validación de evaluaciones",
    "está diseñada y por eso no aparece.",
    "Usuario del organismo es abstracto: el coordinador, el",
    "evaluador y la consulta heredan sus casos de uso.",
]):
    t(nx + 16, ny + 28 + i * 25, texto, size=14, anchor="start")

partes.append("</svg>")
Path(__file__).with_suffix(".svg").write_text("\n".join(partes), encoding="utf-8")
print("escrito", Path(__file__).with_suffix(".svg"))
