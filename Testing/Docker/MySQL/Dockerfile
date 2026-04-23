FROM mysql:8.4

# Setze das Root-Passwort und andere Umgebungsvariablen
ENV MYSQL_ROOT_PASSWORD=root123
ENV MYSQL_DATABASE=raydb
ENV MYSQL_USER=rayuser
ENV MYSQL_PASSWORD=raypass123

# Kopiere Konfigurationsdateien
COPY config/my.cnf /etc/mysql/conf.d/

# Kopiere Initialisierungsskripte
COPY init/ /docker-entrypoint-initdb.d/

# Setze korrekte Berechtigungen
RUN chmod 644 /etc/mysql/conf.d/my.cnf && \
    chmod +x /docker-entrypoint-initdb.d/*.sh

# Exponiere den Standard MySQL Port
EXPOSE 3306

# Verwende das Standard-Entrypoint des MySQL-Images
ENTRYPOINT ["docker-entrypoint.sh"]
CMD ["mysqld"]
