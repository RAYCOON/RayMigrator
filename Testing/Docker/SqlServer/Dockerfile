ARG SOURCE_MICROSOFT=mcr.microsoft.com
FROM ${SOURCE_MICROSOFT}/mssql/server:2022-latest

# Umgebungskonfiguration (nur nicht-geheime Werte)
# MSSQL_SA_PASSWORD, MSSQL_PID und RM_LOGIN_PASSWORD kommen zur Laufzeit
# ueber docker-compose.yml aus der env-Datei. Keine Passwoerter im Image hinterlegen!
ENV ACCEPT_EULA=Y
ENV MSSQL_AGENT_ENABLED=True
ENV TZ=Europe/Berlin
ENV MSSQL_COLLATION=Latin1_General_CI_AS

# TLS-Konfiguration (optional)
# ENV MSSQL_TLS_ENABLED=True
# ENV MSSQL_TLS_CERT=/path/to/certificate.pem
# ENV MSSQL_TLS_KEY=/path/to/key.pem
# ENV MSSQL_TLS_CA=/path/to/ca.pem

# Skripte und Daten kopieren
COPY ./sql-scripts /sql-scripts
COPY ./*.sh /
# RUN chmod +x /entrypoint.sh

# Healthcheck (optional)
HEALTHCHECK --interval=3s --start-period=600s CMD test -e /tmp/app-initialized

# Startbefehl definieren
ENTRYPOINT [ "/bin/bash", "entrypoint.sh" ]
CMD [ "/opt/mssql/bin/sqlservr" ]
