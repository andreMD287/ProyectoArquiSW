#!/bin/sh
set -eu
cd "$(dirname "$0")"
# Python en modo UTF-8 para que los scripts lean bien los acentos también en Windows.
export PYTHONUTF8=1
set -a; . servidor/.env; set +a
# En Windows "python3" puede ser solo el acceso directo de la tienda: se usa el que funcione.
PY=python3
"$PY" -c "import sys" 2>/dev/null || PY=python
"$PY" servidor/scripts/esperar_api.py
"$PY" servidor/scripts/verificar_api.py
"$PY" servidor/scripts/verificar_integracion.py
if [ "${CAMPO_DIFERIDA:-false}" = "true" ]; then
    "$PY" servidor/scripts/verificar_campo.py --url "${MAPAS_URL:-http://127.0.0.1:18085}" --diferida
else
    "$PY" servidor/scripts/verificar_campo.py --url "${MAPAS_URL:-http://127.0.0.1:18085}"
fi
"$PY" servidor/scripts/verificar_reconstruccion.py
"$PY" servidor/scripts/verificar_evolucion.py
"$PY" servidor/scripts/verificar_unificacion.py
"$PY" servidor/scripts/verificar_caracterizacion.py
"$PY" servidor/scripts/verificar_portal.py
"$PY" servidor/scripts/verificar_seguridad.py
