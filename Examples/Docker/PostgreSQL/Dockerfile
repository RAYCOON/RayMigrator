FROM postgres:latest

ENV LANG=en_US.utf8

COPY ./init/*.sql /docker-entrypoint-initdb.d/

EXPOSE 5432

CMD ["postgres"]
