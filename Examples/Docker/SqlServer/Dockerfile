ARG SOURCE_MICROSOFT=mcr.microsoft.com
FROM ${SOURCE_MICROSOFT}/mssql/server:2022-latest

ENV ACCEPT_EULA=Y
ENV MSSQL_SA_PASSWORD="P@ssw0rd!"
ENV MSSQL_PID="Developer"
ENV MSSQL_AGENT_ENABLED=True
ENV TZ=Europe/Berlin
ENV MSSQL_COLLATION=Latin1_General_CI_AS

COPY ./sql-scripts /sql-scripts
COPY ./*.sh /

HEALTHCHECK --interval=3s --start-period=600s CMD test -e /tmp/app-initialized

ENTRYPOINT [ "/bin/bash", "entrypoint.sh" ]
CMD [ "/opt/mssql/bin/sqlservr" ]
