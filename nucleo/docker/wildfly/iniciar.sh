#!/bin/sh
# Arranque del núcleo. Crea el usuario técnico de EJB remoto con EJB_USUARIO y EJB_CLAVE
# del entorno del contenedor, para que la clave no quede dentro de la imagen, y luego arranca
# WildFly igual que la imagen base.
set -eu
: "${EJB_USUARIO:?Falta EJB_USUARIO}"
: "${EJB_CLAVE:?Falta EJB_CLAVE}"
CONF=/opt/jboss/wildfly/standalone/configuration
# Idempotente: si el contenedor se reinicia, se reemplaza la entrada anterior del usuario.
for archivo in application-users.properties application-roles.properties; do
  sed -i "/^${EJB_USUARIO}=/d" "$CONF/$archivo"
done
/opt/jboss/wildfly/bin/add-user.sh -a -u "$EJB_USUARIO" -p "$EJB_CLAVE" --silent
exec /opt/jboss/wildfly/bin/standalone.sh -b 0.0.0.0 "$@"
