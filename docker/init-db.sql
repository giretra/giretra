-- Runs once on first PostgreSQL startup

-- Keycloak database + dedicated user
CREATE DATABASE keycloak;
CREATE USER keycloak WITH PASSWORD 'change_me_keycloak';
GRANT ALL PRIVILEGES ON DATABASE keycloak TO keycloak;
ALTER DATABASE keycloak OWNER TO keycloak;

-- Application database + dedicated user
CREATE DATABASE giretra;
CREATE USER giretra_app WITH PASSWORD 'change_me_giretra';
GRANT ALL PRIVILEGES ON DATABASE giretra TO giretra_app;
ALTER DATABASE giretra OWNER TO giretra_app;
