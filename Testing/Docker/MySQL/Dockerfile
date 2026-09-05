FROM mysql:8.4

# Credentials (MYSQL_ROOT_PASSWORD, MYSQL_DATABASE, MYSQL_USER, MYSQL_PASSWORD)
# are set at runtime via docker-compose.yml from the env file.
# Do not store any passwords in the image!

# Copy configuration files
COPY config/my.cnf /etc/mysql/conf.d/

# Copy initialization scripts
COPY init/ /docker-entrypoint-initdb.d/

# Set correct permissions
RUN chmod 644 /etc/mysql/conf.d/my.cnf && \
    chmod +x /docker-entrypoint-initdb.d/*.sh

# Expose the default MySQL port
EXPOSE 3306

# Use the default entrypoint of the MySQL image
ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["mysqld"]
