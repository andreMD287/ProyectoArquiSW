"""Piezas comunes para los diagramas del SRS dibujados con posiciones fijas (SVG).

Pensados para una página vertical: 1000 px de ancho. Al insertarlos a lo ancho de la página
(16,5 cm), 16 px de texto quedan en unos 7,5 pt.
"""
import math

AZUL, BORDE, TEXTO, GRIS = "#1E3A8A", "#374151", "#111827", "#6B7280"
FUENTE = "Segoe UI, Arial, sans-serif"


class Lienzo:
    def __init__(self, ancho, alto):
        self.w, self.h = ancho, alto
        self.partes = [
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{ancho}" height="{alto}" viewBox="0 0 {ancho} {alto}">',
            '<defs>'
            f'<marker id="abierta" viewBox="0 0 12 12" refX="11" refY="6" markerWidth="11" markerHeight="11" orient="auto">'
            f'<path d="M1,1 L11,6 L1,11" fill="none" stroke="{BORDE}" stroke-width="1.5"/></marker>'
            f'<marker id="llena" viewBox="0 0 12 12" refX="11" refY="6" markerWidth="10" markerHeight="10" orient="auto">'
            f'<path d="M1,1 L11,6 L1,11 Z" fill="{BORDE}"/></marker>'
            f'<marker id="triangulo" viewBox="0 0 16 16" refX="15" refY="8" markerWidth="15" markerHeight="15" orient="auto">'
            f'<path d="M1,1 L15,8 L1,15 Z" fill="#FFFFFF" stroke="{BORDE}" stroke-width="1.5"/></marker>'
            '</defs>',
            f'<rect width="{ancho}" height="{alto}" fill="#FFFFFF"/>',
        ]
        self.uc = {}

    def texto(self, x, y, s, size=16, weight="normal", anchor="middle", style="normal", color=TEXTO):
        s = s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
        self.partes.append(f'<text x="{x}" y="{y}" font-family="{FUENTE}" font-size="{size}" font-weight="{weight}" '
                           f'font-style="{style}" fill="{color}" text-anchor="{anchor}">{s}</text>')

    def lineas_texto(self, x, y, lineas, size=16, alto=None, **kw):
        alto = alto or round(size * 1.3)
        for i, s in enumerate(lineas):
            self.texto(x, y + i * alto, s, size=size, **kw)

    def rect(self, x1, y1, x2, y2, fill="none", stroke=BORDE, ancho=1.5, punteado=False, rx=6):
        extra = ' stroke-dasharray="7 5"' if punteado else ""
        self.partes.append(f'<rect x="{x1}" y="{y1}" width="{x2 - x1}" height="{y2 - y1}" rx="{rx}" fill="{fill}" '
                           f'stroke="{stroke}" stroke-width="{ancho}"{extra}/>')

    def linea(self, puntos, punteada=False, flecha=None, color=BORDE, ancho=1.6):
        d = " ".join(f"{x},{y}" for x, y in puntos)
        extra = ' stroke-dasharray="8 6"' if punteada else ""
        marca = f' marker-end="url(#{flecha})"' if flecha else ""
        self.partes.append(f'<polyline points="{d}" fill="none" stroke="{color}" stroke-width="{ancho}"{extra}{marca}/>')

    # Casos de uso
    def caso(self, clave, cx, cy, rx, ry, *lineas, size=16):
        self.uc[clave] = (cx, cy, rx, ry)
        self.partes.append(f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="#EFF6FF" stroke="{AZUL}" stroke-width="1.5"/>')
        alto = round(size * 1.25)
        y0 = cy - (len(lineas) - 1) * alto / 2 + size * 0.35
        for i, s in enumerate(lineas):
            self.texto(cx, y0 + i * alto, s, size=size)

    def borde(self, clave, hacia):
        cx, cy, rx, ry = self.uc[clave]
        dx, dy = hacia[0] - cx, hacia[1] - cy
        k = 1 / math.sqrt((dx / rx) ** 2 + (dy / ry) ** 2)
        return (round(cx + dx * k, 1), round(cy + dy * k, 1))

    def izq(self, clave):
        cx, cy, rx, ry = self.uc[clave]
        return (cx - rx, cy)

    def der(self, clave):
        cx, cy, rx, ry = self.uc[clave]
        return (cx + rx, cy)

    def dependencia(self, origen, destino, etiqueta, lx, ly, anchor="middle", extra=None, size=14):
        a = self.borde(origen, self.uc[destino][:2])
        b = self.borde(destino, self.uc[origen][:2])
        self.linea([a, b], punteada=True, flecha="abierta")
        self.texto(lx, ly, etiqueta, size=size, anchor=anchor)
        if extra:
            self.texto(lx, ly + size + 3, extra, size=size, anchor=anchor)

    def actor(self, x, y, nombre, abstracto=False, size=15):
        s = f'stroke="{AZUL}" stroke-width="2" fill="none"'
        self.partes.append(f'<circle cx="{x}" cy="{y + 13}" r="13" fill="#DBEAFE" stroke="{AZUL}" stroke-width="2"/>')
        self.partes.append(f'<line x1="{x}" y1="{y + 26}" x2="{x}" y2="{y + 62}" {s}/>')
        self.partes.append(f'<line x1="{x - 22}" y1="{y + 40}" x2="{x + 22}" y2="{y + 40}" {s}/>')
        self.partes.append(f'<line x1="{x}" y1="{y + 62}" x2="{x - 17}" y2="{y + 90}" {s}/>')
        self.partes.append(f'<line x1="{x}" y1="{y + 62}" x2="{x + 17}" y2="{y + 90}" {s}/>')
        for i, s_ in enumerate(nombre.split("\n")):
            self.texto(x, y + 110 + i * (size + 3), s_, size=size, weight="bold", style="italic" if abstracto else "normal")

    def icono_persona(self, cx, cy, color="#FFFFFF"):
        self.partes.append(f'<circle cx="{cx}" cy="{cy}" r="11" fill="{color}"/>')
        self.partes.append(f'<path d="M{cx - 18},{cy + 34} q0,-20 18,-20 q18,0 18,20 Z" fill="{color}"/>')

    def guardar(self, ruta):
        ruta.write_text("\n".join(self.partes + ["</svg>"]), encoding="utf-8")
        print("escrito", ruta)
