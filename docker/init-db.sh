#!/bin/bash
set -e
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    CREATE DATABASE keycloak;
    CREATE USER keycloak WITH PASSWORD '${KEYCLOAK_DB_PASSWORD}';
    GRANT ALL PRIVILEGES ON DATABASE keycloak TO keycloak;
    ALTER DATABASE keycloak OWNER TO keycloak;
    CREATE DATABASE giretra;
    CREATE USER giretra_app WITH PASSWORD '${GIRETRA_DB_PASSWORD}';
    GRANT ALL PRIVILEGES ON DATABASE giretra TO giretra_app;
    ALTER DATABASE giretra OWNER TO giretra_app;
EOSQL
