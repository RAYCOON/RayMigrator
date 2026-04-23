# PostgreSQL Dockerfile - Simplified
FROM postgres:latest

# Set locale
ENV LANG=en_US.utf8

# Copy initialization scripts
COPY ./init/*.sql /docker-entrypoint-initdb.d/

# Expose PostgreSQL port
EXPOSE 5432

# Set default command
CMD ["postgres"]