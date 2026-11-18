"""Genera srs_casos_uso.svg: casos de uso UML de los mínimos a–d, en formato vertical.

Posiciones fijas para que ninguna asociación cruce otra. Los permisos salen del SAD §3.1 y de
los casos de uso de Reconstrucción (AUTORIDAD = COORDINADOR, RESPONSABLE = EVALUADOR,
AUDITOR = CONSULTA). Solo aparecen funciones construidas. Uso: python srs_casos_uso.py
"""
from pathlib import Path
from svg_comun import Lienzo, AZUL, BORDE, GRIS

L = Lienzo(1000, 1335)
L.texto(500, 32, "Casos de uso de los mínimos del proyecto (a–d)", size=21, weight="bold")

# Límite del sistema y un paquete por mínimo
L.rect(120, 50, 880, 1172, stroke=AZUL, ancho=2.2, rx=2)
L.texto(500, 76, "Plataforma para la Gestión de Desastres y Reconstrucción", size=18, weight="bold")


def paquete(x1, y1, x2, y2, titulo):
    L.rect(x1, y1, x2, y2, stroke=BORDE, ancho=1.4)
    L.texto(x1 + 12, y1 + 23, titulo, size=16, weight="bold", anchor="start")


paquete(135, 90, 865, 300, "a. App móvil que recolecta y caracteriza")
paquete(135, 380, 485, 928, "b. Reconstrucción física")
paquete(515, 380, 865, 762, "d. Evaluación de daños")
paquete(135, 962, 865, 1156, "c. Mapa con desastres, evaluaciones y proyectos")

# a
L.caso("CAR", 300, 200, 145, 34, "Caracterizar el desastre", "(también sin conexión)")
L.caso("SYN", 700, 148, 122, 25, "Sincronizar capturas")
L.caso("CON", 700, 252, 112, 31, "Resolver conflicto", "de versión")
L.caso("REG", 300, 336, 108, 23, "Registrar desastre")
# b
L.caso("AVA", 310, 462, 170, 46, "Registrar avance, costo", "y evidencia", "[RN-21: solo el responsable asignado]")
L.caso("INI", 310, 559, 142, 32, "Iniciar reconstrucción", "[RN-01, RN-02]")
L.caso("FOR", 310, 635, 142, 32, "Formular proyectos", "desde evaluaciones")
L.caso("APR", 310, 711, 150, 32, "Aprobar proyecto", "[RN-20: no quien lo formuló]")
L.caso("CER", 310, 787, 142, 32, "Cerrar el desastre", "[RN-04]")
L.caso("AUD", 310, 866, 148, 32, "Consultar seguimiento", "y auditoría")
# d
L.caso("EVD", 690, 456, 165, 35, "Registrar evaluación de daños", "(también sin conexión)")
L.caso("FOT", 605, 590, 82, 32, "Adjuntar", "fotografía")
L.caso("TIP", 778, 590, 80, 32, "Elegir tipo", "de activo")
L.caso("CEV", 690, 712, 126, 27, "Consultar evaluaciones")
# c
L.caso("PAN", 320, 1026, 108, 25, "Visualizar panorama")
L.caso("MAP", 228, 1110, 90, 30, "Consultar mapa", "general")
L.caso("TAB", 412, 1110, 82, 30, "Consultar tablero", "del desastre")

# Actores
L.actor(64, 550, "COORDINADOR", size=14)
L.actor(944, 300, "EVALUADOR", size=14)
L.actor(64, 860, "CONSULTA", size=14)
L.actor(500, 1192, "Usuario del organismo", abstracto=True, size=15)

# COORDINADOR: todas sus asociaciones llegan al extremo izquierdo de cada caso (ninguna cruza)
coo = (88, 592)
for clave in ("INI", "FOR", "APR", "CER", "AUD"):
    L.linea([coo, L.izq(clave)])
# Registrar desastre y Caracterizar suben por el margen, fuera de los paquetes
L.linea([(64, 550), (64, 200), L.izq("CAR")])
L.linea([(74, 555), (100, 336), L.izq("REG")])
# CONSULTA
L.linea([(88, 902), L.izq("AUD")])
# EVALUADOR: directas a Sincronizar y a Registrar evaluación; por pasillos a Caracterizar y a Avance
L.linea([(930, 312), L.der("SYN")])
L.linea([(920, 346), (430, 346), L.borde("CAR", (430, 300))])
L.linea([(920, 366), (462, 366), L.borde("AVA", (462, 300))])
L.linea([(922, 372), L.der("EVD")])
# Usuario del organismo: consultas comunes a los tres roles
L.linea([(486, 1206), L.borde("MAP", (486, 1206))])
L.linea([(488, 1214), L.borde("TAB", (488, 1214))])
L.linea([(500, 1192), (500, 712), L.izq("CEV")])

# include / extend
L.dependencia("CON", "SYN", "«extend»", 688, 196, anchor="end", extra="[respuesta 409]")
L.dependencia("EVD", "TIP", "«include»", 766, 524, anchor="start")
L.dependencia("FOT", "EVD", "«extend»", 620, 524, anchor="end")
L.dependencia("MAP", "PAN", "«include»", 214, 1066, anchor="end")
L.dependencia("TAB", "PAN", "«include»", 426, 1066, anchor="start")

# Generalización: los tres roles son Usuario del organismo (triángulo hueco hacia el padre)
L.linea([(42, 592), (22, 592), (22, 1238), (64, 1238)])
L.linea([(64, 990), (64, 1238), (476, 1238)], flecha="triangulo")
L.linea([(944, 420), (944, 1238), (524, 1238)], flecha="triangulo")

# Nota
nx, ny = 592, 1002
L.partes.append(f'<path d="M{nx},{ny} h262 v146 h-262 Z" fill="#FFFBEB" stroke="{GRIS}" stroke-width="1.2"/>')
L.lineas_texto(nx + 12, ny + 24, [
    "Entre corchetes: reglas de",
    "negocio del SAD (RN-01 a RN-21).",
    "Solo funciones construidas: la",
    "validación de evaluaciones está",
    "diseñada y no aparece. Los tres",
    "roles heredan de Usuario del",
    "organismo.",
], size=14, anchor="start", alto=19)

L.guardar(Path(__file__).with_suffix(".svg"))
