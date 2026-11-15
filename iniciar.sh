#!/bin/sh
set -eu
cd "$(dirname "$0")"
# Python en modo UTF-8 para que los scripts lean bien los acentos también en Windows.
export PYTHONUTF8=1
# Secretos fuera del repositorio: Compose los lee de servidor/.env (no se versiona).
if [ ! -f servidor/.env ]; then
  cp .env.ejemplo servidor/.env
  printf 'Se creó servidor/.env con los valores de laboratorio de .env.ejemplo; cámbialos fuera del laboratorio.
'
fi
set -a; . servidor/.env; set +a
# En Windows "python3" puede ser solo el acceso directo de la tienda: se usa el que funcione.
PY=python3
"$PY" -c "import sys" 2>/dev/null || PY=python
docker compose -f servidor/compose.yaml up -d --wait database
for archivo in nucleo/migraciones/V*.sql; do
  docker compose -f servidor/compose.yaml exec -T database psql -v ON_ERROR_STOP=1 -U pgdr -d pgdrdb < "$archivo"
done
docker compose -f servidor/compose.yaml up -d --build
"$PY" servidor/scripts/esperar_api.py
"$PY" servidor/scripts/esperar_nucleo.py
"$PY" servidor/scripts/esperar_taller3.py
"$PY" servidor/scripts/esperar_reconstruccion.py
printf '\nSolución disponible: http://127.0.0.1:18085\n'
