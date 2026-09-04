FROM mysql:8.4

# Zugangsdaten (MYSQL_ROOT_PASSWORD, MYSQL_DATABASE, MYSQL_USER, MYSQL_PASSWORD)
# werden zur Laufzeit ueber docker-compose.yml aus der env-Datei gesetzt.
# Keine Passwoerter im Image hinterlegen!

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
