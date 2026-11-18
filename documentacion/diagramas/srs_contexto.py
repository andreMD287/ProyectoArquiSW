"""Genera srs_contexto.svg: diagrama de contexto del SRS, con la plataforma como caja negra.

Nombres, mecanismos y estado de cada sistema externo tomados del SAD §3.1 y §3.2.
Formato vertical para una página. Uso: python srs_contexto.py
"""
from pathlib import Path
from svg_comun import Lienzo, AZUL, BORDE, GRIS

L = Lienzo(1000, 1335)
L.texto(500, 34, "Diagrama de contexto", size=22, weight="bold")
L.texto(500, 60, "La plataforma como caja negra: quién la usa y con qué sistemas se integra", size=15, color=GRIS)

PERSONA, PLATAF, EXT_C, EXT_D = "#08427B", "#1D4ED8", "#D1D5DB", "#F3F4F6"


def persona(x1, y1, x2, y2, nombre, rol, canal, disenada=False):
    cx = (x1 + x2) / 2
    if disenada:
        L.rect(x1, y1, x2, y2, fill=EXT_D, stroke=GRIS, punteado=True)
        L.icono_persona(cx, y1 + 26, color="#374151")
        color, tenue = "#111827", "#374151"
    else:
        L.rect(x1, y1, x2, y2, fill=PERSONA, stroke=PERSONA)
        L.icono_persona(cx, y1 + 26)
        color, tenue = "#FFFFFF", "#DBEAFE"
    L.texto(cx, y1 + 90, nombre, size=17, weight="bold", color=color)
    L.texto(cx, y1 + 112, rol, size=15, color=tenue)
    L.texto(cx, y1 + 133, canal, size=15, style="italic", color=tenue)


def externo(x1, y1, x2, y2, nombre, detalle=(), construido=False):
    if construido:
        L.rect(x1, y1, x2, y2, fill=EXT_C, stroke=BORDE)
    else:
        L.rect(x1, y1, x2, y2, fill=EXT_D, stroke=GRIS, punteado=True)
    cx = (x1 + x2) / 2
    n = len(nombre) + len(detalle)
    y = (y1 + y2) / 2 - (n - 1) * 10 + 6
    for s in nombre:
        L.texto(cx, y, s, size=16, weight="bold")
        y += 20
    for s in detalle:
        L.texto(cx, y, s, size=15, style="italic", color="#374151")
        y += 20


def grupo(x1, y1, x2, y2, *titulo, abajo=False):
    L.rect(x1, y1, x2, y2, stroke=GRIS, punteado=True, ancho=1.3)
    for i, s in enumerate(titulo):
        y = (y2 - 12 - (len(titulo) - 1 - i) * 19) if abajo else (y1 + 22 + i * 19)
        L.texto(x1 + 12, y, s, size=15, weight="bold", anchor="start", color="#374151")


# Actores, arriba
grupo(20, 78, 612, 262, "Usuarios de los organismos")
persona(32, 104, 212, 250, "Coordinador", "Rol COORDINADOR", "portal web")
persona(222, 104, 402, 250, "Evaluador de campo", "Rol EVALUADOR", "app de campo y portal")
persona(412, 104, 592, 250, "Consulta", "Rol CONSULTA", "portal web")
persona(626, 104, 798, 250, "Ciudadano", "sin sesión", "consulta pública", disenada=True)
L.rect(810, 104, 980, 250, fill=EXT_C, stroke=BORDE)
L.lineas_texto(895, 150, ["Sistemas que", "no usan REST"], size=17, weight="bold")
L.lineas_texto(895, 200, ["inician sesión por", "SOAP o EJB remoto"], size=15, style="italic", color="#374151", alto=20)

# Plataforma, al centro
L.rect(330, 440, 670, 640, fill=PLATAF, stroke=AZUL, rx=8)
L.lineas_texto(500, 496, ["Plataforma para la Gestión", "de Desastres y Reconstrucción"], size=19, weight="bold", color="#FFFFFF", alto=25)
L.lineas_texto(500, 566, ["registra y caracteriza desastres,", "evalúa daños, gestiona la", "reconstrucción y avisa"],
               size=15, color="#DBEAFE", alto=20)

# Personas hacia la plataforma
L.linea([(122, 250), (400, 440)], flecha="llena")
L.linea([(312, 250), (455, 440)], flecha="llena")
L.linea([(502, 250), (510, 440)], flecha="llena")
L.linea([(712, 250), (570, 440)], punteada=True, flecha="llena")
L.linea([(895, 250), (640, 440)], flecha="llena")

# SE-01 a la izquierda: entran a la plataforma
grupo(20, 340, 226, 790, "SE-01 Alerta temprana")
L.texto(32, 384, "envían eventos por", size=15, style="italic", anchor="start", color="#374151")
L.texto(32, 403, "cola o webhook", size=15, style="italic", anchor="start", color="#374151")
externo(30, 425, 216, 525, ["SE-01a Sistemas de", "alerta temprana"])
externo(30, 545, 216, 645, ["SE-01b Estaciones", "meteorológicas"])
externo(30, 665, 216, 765, ["SE-01c Sensores", "sísmicos"])
L.linea([(216, 475), (330, 490)], punteada=True, flecha="llena")
L.linea([(216, 595), (330, 545)], punteada=True, flecha="llena")
L.linea([(216, 715), (330, 600)], punteada=True, flecha="llena")

# A la derecha: información y comunicación; gestión administrativa y obras
grupo(750, 400, 980, 670, "Información y", "comunicación")
externo(762, 450, 968, 555, ["SE-02 Sistemas de", "información geográfica"], ["geometrías · REST"])
externo(762, 565, 968, 660, ["SE-08 Comunicación", "y notificación"], ["avisos y correo", "cola y SMTP"], construido=True)
grupo(750, 688, 980, 940, "Gestión administrativa", "y obras")
externo(762, 734, 968, 830, ["SE-06 Administrativos", "y financieros"], ["compromiso presupuestal", "cola diferida"])
externo(762, 840, 968, 932, ["SE-07 Proyectos de", "infraestructura"], ["proyectos y avances", "cola diferida"])
L.linea([(670, 480), (762, 502)], punteada=True, flecha="llena")
L.linea([(670, 540), (762, 612)], flecha="llena")
L.linea([(670, 598), (762, 782)], punteada=True, flecha="llena")
L.linea([(670, 630), (762, 886)], punteada=True, flecha="llena")

# Abajo: atención, recursos y personas
grupo(20, 970, 980, 1178, "Atención, recursos y personas", abajo=True)
externo(34, 990, 330, 1136, ["SE-03 Hospitalarios"], ["avisa el desastre y recibe", "la capacidad disponible", "cola diferida; consulta cada 60 s"])
externo(352, 990, 648, 1136, ["SE-04 Gestión de recursos"], ["dueño de recursos y donaciones", "disponibilidad", "consulta cada 5 min"])
externo(670, 990, 966, 1136, ["SE-05 Identificación y", "atención de personas"], ["dueño de los datos de personas", "solo identificadores · REST y cola"])
L.linea([(420, 640), (182, 990)], punteada=True, flecha="llena")
L.linea([(500, 640), (500, 990)], punteada=True, flecha="llena")
L.linea([(600, 640), (760, 990)], punteada=True, flecha="llena")

# Leyenda
y = 1208
L.texto(20, y, "Leyenda", size=16, weight="bold", anchor="start")
cuadros = [(PERSONA, PERSONA, False, "Persona con un rol"), (PLATAF, AZUL, False, "Plataforma (construida)"),
           (EXT_C, BORDE, False, "Externo con integración construida"), (EXT_D, GRIS, True, "Externo o actor diseñado")]
x = 20
for i, (fill, stroke, punt, s) in enumerate(cuadros):
    xx, yy = (20 + (i % 2) * 480, y + 16 + (i // 2) * 30)
    L.rect(xx, yy, xx + 34, yy + 20, fill=fill, stroke=stroke, punteado=punt, rx=3)
    L.texto(xx + 44, yy + 16, s, size=15, anchor="start")
L.linea([(20, y + 92), (74, y + 92)], flecha="llena")
L.texto(84, y + 97, "integración construida", size=15, anchor="start")
L.linea([(500, y + 92), (554, y + 92)], punteada=True, flecha="llena")
L.texto(564, y + 97, "integración diseñada, sin adaptador", size=15, anchor="start")
L.texto(20, y + 118, "La flecha va en el sentido de la solicitud o del mensaje. El recuadro punteado agrupa sistemas afines.",
        size=14, anchor="start", color="#374151")

L.guardar(Path(__file__).with_suffix(".svg"))
