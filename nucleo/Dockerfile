
# Etapa 1: compilar nuestra aplicación
FROM maven:3.9-eclipse-temurin-21 AS build

WORKDIR /app

COPY pom.xml .
COPY src ./src

RUN --mount=type=cache,target=/root/.m2 mvn -B clean verify
# Obtener el controlador JDBC de PostgreSQL
RUN --mount=type=cache,target=/root/.m2 mvn -B org.apache.maven.plugins:maven-dependency-plugin:3.7.1:copy \
    -Dartifact=org.postgresql:postgresql:42.7.8 \
    -DoutputDirectory=/tmp/driver \
    -Dmdep.stripVersion=true

# Etapa 2: servidor de aplicaciones
FROM quay.io/wildfly/wildfly:41.0.1.Final-jdk21

# Instalar módulo JDBC
COPY --chown=jboss:0 --from=build \
    /tmp/driver/postgresql.jar \
    /opt/jboss/wildfly/modules/system/layers/base/org/postgresql/main/postgresql.jar

COPY --chown=jboss:0 \
    docker/postgresql/module.xml \
    /opt/jboss/wildfly/modules/system/layers/base/org/postgresql/main/module.xml

# Configurar datasource antes de desplegar la aplicación
COPY --chown=jboss:0 \
    docker/wildfly/configure-datasource.cli \
    /tmp/configure-datasource.cli

RUN /opt/jboss/wildfly/bin/jboss-cli.sh \
    --file=/tmp/configure-datasource.cli

# Desplegar aplicación
COPY --chown=jboss:0 --from=build \
    /app/target/plataforma-desastres.war \
    /opt/jboss/wildfly/standalone/deployments/plataforma-desastres.war

# Usuario técnico de EJB remoto (application realm, como en el taller de login): se crea al
# arrancar con EJB_USUARIO y EJB_CLAVE del entorno, así la clave no queda dentro de la imagen.
COPY --chown=jboss:0 docker/wildfly/iniciar.sh /opt/jboss/wildfly/bin/pgdr-iniciar.sh
RUN sed -i 's/\r$//' /opt/jboss/wildfly/bin/pgdr-iniciar.sh && chmod 0755 /opt/jboss/wildfly/bin/pgdr-iniciar.sh
CMD ["/opt/jboss/wildfly/bin/pgdr-iniciar.sh"]

EXPOSE 8080
