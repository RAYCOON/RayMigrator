ARG SOURCE_MICROSOFT=mcr.microsoft.com
FROM ${SOURCE_MICROSOFT}/mssql/server:2022-latest

# Environment configuration (non-secret values only)
# MSSQL_SA_PASSWORD, MSSQL_PID and RM_LOGIN_PASSWORD are provided at runtime
# via docker-compose.yml from the env file. Do not store any passwords in the image!
ENV ACCEPT_EULA=Y
ENV MSSQL_AGENT_ENABLED=True
ENV TZ=Europe/Berlin
ENV MSSQL_COLLATION=Latin1_General_CI_AS

# TLS configuration (optional)
# ENV MSSQL_TLS_ENABLED=True
# ENV MSSQL_TLS_CERT=/path/to/certificate.pem
# ENV MSSQL_TLS_KEY=/path/to/key.pem
# ENV MSSQL_TLS_CA=/path/to/ca.pem

# Copy scripts and data
COPY ./sql-scripts /sql-scripts
COPY ./*.sh /
# RUN chmod +x /entrypoint.sh

# Healthcheck (optional)
HEALTHCHECK --interval=3s --start-period=600s CMD test -e /tmp/app-initialized

# Define the start command
ENTRYPOINT [ "/bin/bash", "entrypoint.sh" ]
CMD [ "/opt/mssql/bin/sqlservr" ]
