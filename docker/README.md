# Docker Infrastructure — Giretra

This folder contains the Docker Compose setup for running PostgreSQL and Keycloak locally.

## What's Included

| File | Purpose |
|------|---------|
| `docker-compose.yml` | PostgreSQL 17 + Keycloak 26.1 |
| `init-db.sql` | Creates `keycloak` and `giretra` databases with dedicated users |
| `.env.example` | Template for secrets and social provider credentials |
| `keycloak/realm-export.json` | Auto-imports the `giretra` realm with clients and roles |

## Quick Start

```bash
# 1. Create your .env from the template
cp .env.example .env

# 2. Edit .env — set real passwords
#    IMPORTANT: KEYCLOAK_DB_PASSWORD must match the password in init-db.sql
#    Update both if you change either one.

# 3. Start services
docker compose up -d

# 4. Wait for Keycloak to be ready
docker compose logs -f keycloak
# Look for: "Keycloak <version> started"

# 5. Access Keycloak admin console
#    http://localhost:8080/admin
#    Login: admin / <your KEYCLOAK_ADMIN_PASSWORD>
```

## Next Steps

### 1. Sync passwords between `.env` and `init-db.sql`

The `init-db.sql` script runs only on the **first** PostgreSQL startup. The passwords hardcoded there (`change_me_keycloak`, `change_me_giretra`) must match what you put in `.env`. Either:
- Update `init-db.sql` with your chosen passwords **before** first `docker compose up`, or
- Keep the defaults in both places for local dev

### 2. Configure Social Identity Providers

The realm export sets up the `giretra` realm, clients, and roles automatically. Social login providers (Google, GitHub) must be configured manually in the Keycloak admin console because they require secrets that shouldn't be in the realm export.

1. Create OAuth apps on each provider (see `CONFIGURE-KEYCLOAK.md` for redirect URIs)
2. Fill in the credentials in your `.env`
3. Go to **Keycloak Admin > giretra realm > Identity Providers**
4. Add Google and/or GitHub with the credentials from your `.env`

### 3. Apply the Application Database Schema

The `giretra` database is created empty. The schema from `DATA-DESIGN-PLAN.md` still needs to be applied. Options:
- Add SQL migration files and run them manually
- Use Entity Framework Core migrations from `Giretra.Web`
- Add a `schema.sql` volume mount to `init-db.sql` (for initial setup only)

### 4. Configure the Backend (`Giretra.Web`)

Update `Giretra.Web` configuration (e.g. `appsettings.Development.json`) to point to:
- **PostgreSQL:** `Host=localhost;Port=5432;Database=giretra;Username=giretra_app;Password=<your password>`
- **Keycloak:** `Authority=http://localhost:8080/realms/giretra`

### 5. Production Hardening

Before deploying to production:

| Item | Action |
|------|--------|
| Keycloak mode | Change `start-dev` to `start` (requires HTTPS) |
| HTTPS | Terminate TLS at a reverse proxy or configure `KC_HTTPS_*` env vars |
| Hostname | Set `KC_HOSTNAME` to your actual domain (e.g. `auth.giretra.mg`) |
| Redirect URIs | Update client redirect URIs and social provider callbacks to production URLs |
| Secrets | Use a secrets manager instead of `.env` files |
| Admin console | Restrict network access or disable with `KC_FEATURES_DISABLED=admin2` |

## Useful Commands

```bash
# Stop services (preserves data)
docker compose down

# Stop and wipe all data (fresh start)
docker compose down -v

# View PostgreSQL logs
docker compose logs postgres

# Connect to PostgreSQL
docker compose exec postgres psql -U giretra_admin -d giretra

# View Keycloak logs
docker compose logs -f keycloak
```
