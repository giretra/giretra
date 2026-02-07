# Configure Keycloak - Giretra

> Keycloak running as a Docker container, backed by the same PostgreSQL instance as the application database.

---

## Architecture

```
┌─────────────┐     ┌─────────────────┐     ┌──────────────┐
│  Browser     │────▶│  Giretra.Web    │────▶│  PostgreSQL   │
│  (Angular)   │     │  :5000          │     │  :5432        │
└──────┬───────┘     └─────────────────┘     │               │
       │                                     │  DB: giretra   │
       │             ┌─────────────────┐     │  DB: keycloak  │
       └────────────▶│  Keycloak       │────▶│               │
                     │  :8080          │     └──────────────┘
                     └─────────────────┘
```

- One PostgreSQL container, **two databases**: `giretra` (app) and `keycloak` (Keycloak internal)
- Keycloak manages its own schema in its own database — no cross-contamination
- The app syncs relevant user fields from Keycloak tokens into `users` table (see DATA-DESIGN-PLAN.md)

---

## Prerequisites

### Social Provider Developer Accounts

You need to create OAuth apps on each provider **before** configuring Keycloak.

#### Google

- Console: https://console.cloud.google.com/apis/credentials
- Create an **OAuth 2.0 Client ID** (Web application type)
- Authorized redirect URI: `http://localhost:8080/realms/giretra/broker/google/endpoint`
- You get: **Client ID** + **Client Secret**
- Enable the **Google+ API** or **People API** (for profile info)

#### Facebook

- Console: https://developers.facebook.com/apps/
- Create a **Consumer** app
- Add the **Facebook Login** product
- Valid OAuth Redirect URI: `http://localhost:8080/realms/giretra/broker/facebook/endpoint`
- You get: **App ID** + **App Secret**
- Required permissions: `email`, `public_profile`
- Note: App must be in **Live** mode for non-test users (Development mode works for app admins/testers only)

#### GitHub

- Console: https://github.com/settings/developers
- Create a new **OAuth App**
- Authorization callback URL: `http://localhost:8080/realms/giretra/broker/github/endpoint`
- You get: **Client ID** + **Client Secret**
- Scopes requested by Keycloak: `user:email`, `read:user`

---

## Docker Compose

```yaml
# docker-compose.yml

services:
  postgres:
    image: postgres:17
    container_name: giretra-postgres
    environment:
      POSTGRES_USER: giretra_admin
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./init-db.sql:/docker-entrypoint-initdb.d/init-db.sql
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U giretra_admin"]
      interval: 5s
      timeout: 3s
      retries: 5

  keycloak:
    image: quay.io/keycloak/keycloak:26.1
    container_name: giretra-keycloak
    command: start-dev --import-realm
    environment:
      # Admin console
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: ${KEYCLOAK_ADMIN_PASSWORD}

      # Database — same PostgreSQL, different database
      KC_DB: postgres
      KC_DB_URL: jdbc:postgresql://postgres:5432/keycloak
      KC_DB_USERNAME: keycloak
      KC_DB_PASSWORD: ${KEYCLOAK_DB_PASSWORD}

      # Hostname
      KC_HOSTNAME: localhost
      KC_HTTP_PORT: 8080
    volumes:
      - ./keycloak/realm-export.json:/opt/keycloak/data/import/realm-export.json
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy

volumes:
  pgdata:
```

---

## Database Initialization Script

```sql
-- init-db.sql
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
```

**Important:** The `POSTGRES_PASSWORD` in compose is for the superuser. Keycloak and the app each get their own restricted user. Replace passwords via `.env`.

---

## Environment File

```env
# .env (git-ignored)

POSTGRES_PASSWORD=change_me_super
KEYCLOAK_ADMIN_PASSWORD=change_me_admin
KEYCLOAK_DB_PASSWORD=change_me_keycloak
GIRETRA_DB_PASSWORD=change_me_giretra

# Social providers
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
FACEBOOK_APP_ID=
FACEBOOK_APP_SECRET=
GITHUB_CLIENT_ID=
GITHUB_CLIENT_SECRET=
```

---

## Keycloak Realm Configuration

### Realm: `giretra`

| Setting | Value |
|---------|-------|
| Realm name | `giretra` |
| Display name | Giretra |
| User registration | Enabled (players can self-register) |
| Email as username | No (separate username) |
| Login with email | Yes |
| Remember me | Yes |
| Registration email as username | No |

### Client: `giretra-web`

| Setting | Value |
|---------|-------|
| Client ID | `giretra-web` |
| Client type | OpenID Connect |
| Client authentication | Off (public client — SPA) |
| Standard flow | Enabled |
| Direct access grants | Disabled |
| Valid redirect URIs | `http://localhost:4200/*` (Angular dev) |
| Web origins | `http://localhost:4200` |
| Post logout redirect URIs | `http://localhost:4200` |

### Client: `giretra-api`

| Setting | Value |
|---------|-------|
| Client ID | `giretra-api` |
| Client type | OpenID Connect |
| Client authentication | On (confidential — backend) |
| Service accounts | Enabled (for backend-to-Keycloak admin calls if needed) |
| Valid redirect URIs | `http://localhost:5000/*` |

### Roles

| Role | Description |
|------|-------------|
| `normal` | Default role assigned on registration |
| `admin` | Full access — manage users, bots, game config |

Assign `normal` as the **default realm role** so every new user gets it automatically.

### Identity Providers

Configure under **Realm > Identity Providers**:

#### Google

| Field | Value |
|-------|-------|
| Alias | `google` |
| Client ID | `${GOOGLE_CLIENT_ID}` |
| Client Secret | `${GOOGLE_CLIENT_SECRET}` |
| Default scopes | `openid email profile` |
| Trust email | Yes (Google verifies emails) |
| First login flow | `first broker login` (default) |

#### Facebook

| Field | Value |
|-------|-------|
| Alias | `facebook` |
| Client ID | `${FACEBOOK_APP_ID}` |
| Client Secret | `${FACEBOOK_APP_SECRET}` |
| Default scopes | `email public_profile` |
| Trust email | Yes |
| First login flow | `first broker login` (default) |

#### GitHub

| Field | Value |
|-------|-------|
| Alias | `github` |
| Client ID | `${GITHUB_CLIENT_ID}` |
| Client Secret | `${GITHUB_CLIENT_SECRET}` |
| Default scopes | `user:email read:user` |
| Trust email | No (GitHub allows unverified emails) |
| First login flow | `first broker login` (default) |

### First Broker Login Flow

The default `first broker login` flow handles what happens when a user logs in via a social provider for the first time:

1. **Review profile** — user confirms/edits username and email
2. **Create user if not exists** — auto-provisions a Keycloak user
3. **Link account if email matches** — if a local account already exists with the same email, prompts user to link

This is Keycloak's built-in default. No custom flow needed.

### Token Mappers

Add these to the `giretra-web` client scope so the app gets what it needs in the JWT:

| Mapper | Type | Token claim |
|--------|------|-------------|
| Realm roles | User Realm Role | `realm_access.roles` (already default) |
| Username | User Property | `preferred_username` |
| Email | User Property | `email` |
| Full name | User Attribute | `name` |

---

## What the App Receives (JWT Claims)

After login (social or direct), the Angular app gets an access token with:

```json
{
  "sub": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "preferred_username": "rakoto",
  "email": "rakoto@example.com",
  "name": "Rakoto Be",
  "realm_access": {
    "roles": ["normal"]
  },
  "azp": "giretra-web"
}
```

The backend (`Giretra.Web`) validates this token and syncs to the `users` table:

| JWT claim | `users` column |
|-----------|---------------|
| `sub` | `keycloak_id` |
| `preferred_username` | `username` |
| `email` | `email` |
| `name` | `display_name` |
| `realm_access.roles` | `role` |

---

## Redirect URI Summary

All three providers need the same pattern for their redirect URIs:

| Provider | Redirect URI |
|----------|-------------|
| Google | `http://localhost:8080/realms/giretra/broker/google/endpoint` |
| Facebook | `http://localhost:8080/realms/giretra/broker/facebook/endpoint` |
| GitHub | `http://localhost:8080/realms/giretra/broker/github/endpoint` |

For production, replace `localhost:8080` with your Keycloak domain (e.g. `auth.giretra.mg`).

---

## Startup Sequence

```bash
# 1. Copy and fill in secrets
cp .env.example .env
# Edit .env with real passwords and social provider credentials

# 2. Start infrastructure
docker compose up -d

# 3. Wait for Keycloak to be ready
docker compose logs -f keycloak  # look for "Keycloak started"

# 4. If not using realm import, configure manually at:
#    http://localhost:8080/admin (admin / your password)

# 5. Start the app
dotnet run --project Giretra.Web
```

---

## Production Considerations

| Topic | Dev | Production |
|-------|-----|------------|
| Keycloak command | `start-dev` | `start` (requires HTTPS) |
| HTTPS | Not needed | Required — terminate TLS at reverse proxy or configure KC_HTTPS_* |
| Hostname | `localhost` | `KC_HOSTNAME=auth.yourdomain.com` |
| Redirect URIs | `localhost:4200` | Your production domain |
| Facebook app mode | Development | Live (requires App Review for `email` permission) |
| DB passwords | `.env` file | Secrets manager (Vault, AWS SSM, etc.) |
| Admin console | Exposed | Restrict access via network rules or disable with `KC_FEATURES_DISABLED=admin2` |
