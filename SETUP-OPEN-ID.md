# Setup OpenID Connect - Social Login Providers

> Step-by-step guide to create OAuth apps on Google, Facebook, and GitHub, then configure them as Keycloak identity providers for Giretra.

---

## Domain Layout

| Service | Local | Production |
|---------|-------|------------|
| Angular SPA | `http://localhost:4200` | `https://play.giretra.com` |
| .NET API | `http://localhost:5000` | `https://api.giretra.com` |
| Keycloak | `http://localhost:8080` | `https://auth.giretra.com` |

---

## Redirect URIs

All three providers need redirect URIs pointing to your Keycloak instance. Add **both** local and production URIs to each provider.

### Social provider redirect URIs (provider → Keycloak)

| Provider | Local | Production |
|----------|-------|------------|
| Google   | `http://localhost:8080/realms/giretra/broker/google/endpoint` | `https://auth.giretra.com/realms/giretra/broker/google/endpoint` |
| Facebook | `http://localhost:8080/realms/giretra/broker/facebook/endpoint` | `https://auth.giretra.com/realms/giretra/broker/facebook/endpoint` |
| GitHub   | `http://localhost:8080/realms/giretra/broker/github/endpoint` | `https://auth.giretra.com/realms/giretra/broker/github/endpoint` |

> **GitHub limitation**: GitHub OAuth Apps only support a single callback URL. Register **two separate OAuth apps** (one for local, one for production), or use the production URL and tunnel locally with `ngrok`.

### Keycloak client redirect URIs (Keycloak → app)

**Client `giretra-web`** (public SPA):

| Setting | Values |
|---------|--------|
| Valid Redirect URIs | `http://localhost:4200/*`, `https://play.giretra.com/*` |
| Web Origins | `http://localhost:4200`, `https://play.giretra.com` |
| Post Logout Redirect URIs | `http://localhost:4200`, `https://play.giretra.com` |

**Client `giretra-api`** (confidential backend):

| Setting | Values |
|---------|--------|
| Valid Redirect URIs | `http://localhost:5000/*`, `https://api.giretra.com/*` |

---

## 1. Google

### Create OAuth 2.0 credentials

1. Go to [Google Cloud Console - Credentials](https://console.cloud.google.com/apis/credentials)
2. Create a project (or select an existing one)
3. Click **Create Credentials** > **OAuth client ID**
4. If prompted, configure the **OAuth consent screen** first:
   - User type: **External**
   - App name: `Giretra`
   - User support email: your email
   - Authorized domains: `localhost`, `giretra.com`
   - Scopes: add `email`, `profile`, `openid`
   - Save and continue through test users
5. Back on Credentials, click **Create Credentials** > **OAuth client ID**
6. Application type: **Web application**
7. Name: `Giretra`
8. Authorized redirect URIs — add both:
   ```
   http://localhost:8080/realms/giretra/broker/google/endpoint
   https://auth.giretra.com/realms/giretra/broker/google/endpoint
   ```
9. Click **Create**
10. Copy the **Client ID** and **Client Secret**

### Enable required APIs

1. Go to [APIs & Services](https://console.cloud.google.com/apis/library)
2. Search for and enable **Google People API** (needed for profile info)

### Save to `.env`

```env
GOOGLE_CLIENT_ID=123456789.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-xxxxxxxxxxxxxxxx
```

---

## 2. Facebook

### Create a Facebook App

1. Go to [Meta for Developers](https://developers.facebook.com/apps/)
2. Click **Create App**
3. Select use case: **Authenticate and request data from users with Facebook Login**
4. App name: `Giretra`
5. Click **Create App**

### Configure Facebook Login

1. In your app dashboard, find **Facebook Login** and click **Set Up**
2. Select **Web** platform
3. Skip the quickstart, go to **Facebook Login** > **Settings** in the left sidebar
4. Under **Valid OAuth Redirect URIs**, add both:
   ```
   http://localhost:8080/realms/giretra/broker/facebook/endpoint
   https://auth.giretra.com/realms/giretra/broker/facebook/endpoint
   ```
5. Save Changes

### Get your credentials

1. Go to **App Settings** > **Basic** in the left sidebar
2. Copy the **App ID** and **App Secret** (click Show to reveal)

### App mode

- In **Development** mode, only users listed under **App Roles** > **Test Users** can log in
- To allow all users: go to the top bar toggle and switch to **Live** mode
- Going Live requires completing Facebook's **App Review** for the `email` permission

### Save to `.env`

```env
FACEBOOK_APP_ID=123456789012345
FACEBOOK_APP_SECRET=abcdef0123456789abcdef0123456789
```

---

## 3. GitHub

### Create an OAuth App

1. Go to [GitHub Developer Settings](https://github.com/settings/developers)
2. Click **OAuth Apps** > **New OAuth App**
3. Fill in:
   - Application name: `Giretra (local)` (or `Giretra` for production)
   - Homepage URL: `http://localhost:4200` (or `https://play.giretra.com`)
   - Authorization callback URL: `http://localhost:8080/realms/giretra/broker/github/endpoint` (or `https://auth.giretra.com/realms/giretra/broker/github/endpoint`)
4. Click **Register application**
5. Copy the **Client ID**
6. Click **Generate a new client secret** and copy it immediately (shown only once)

> **Note**: GitHub only allows one callback URL per OAuth App. Create **two apps** — one for local development, one for production — with separate credentials in your `.env` files.

### Save to `.env`

```env
GITHUB_CLIENT_ID=Iv1.xxxxxxxxxxxx
GITHUB_CLIENT_SECRET=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

---

## Configure Keycloak

After filling in your `.env` and starting the containers, configure each provider in Keycloak.

```bash
# Create the network (once) and start
docker network create tetezana
docker compose up -d
```

### Access the admin console

1. Open Keycloak admin via your reverse proxy (e.g. `https://auth.giretra.com/admin` or `http://localhost:8080/admin` if proxied locally)
2. Log in with `admin` / your `KEYCLOAK_ADMIN_PASSWORD`
3. Select the `giretra` realm (create it first if using manual setup instead of realm import)

> **Note:** Keycloak is not exposed on the host. It is only reachable through the `tetezana` docker network. Your reverse proxy must forward to `giretra-keycloak:8080`.

### Add Google identity provider

1. Go to **Identity Providers** > **Add provider** > **Google**
2. Fill in:

| Field | Value |
|-------|-------|
| Alias | `google` |
| Client ID | Your Google Client ID |
| Client Secret | Your Google Client Secret |
| Default Scopes | `openid email profile` |
| Trust Email | ON (Google verifies emails) |
| First Login Flow | `first broker login` |

3. Click **Save**

### Add Facebook identity provider

1. Go to **Identity Providers** > **Add provider** > **Facebook**
2. Fill in:

| Field | Value |
|-------|-------|
| Alias | `facebook` |
| Client ID | Your Facebook App ID |
| Client Secret | Your Facebook App Secret |
| Default Scopes | `email public_profile` |
| Trust Email | ON |
| First Login Flow | `first broker login` |

3. Click **Save**

### Add GitHub identity provider

1. Go to **Identity Providers** > **Add provider** > **GitHub**
2. Fill in:

| Field | Value |
|-------|-------|
| Alias | `github` |
| Client ID | Your GitHub Client ID |
| Client Secret | Your GitHub Client Secret |
| Default Scopes | `user:email read:user` |
| Trust Email | OFF (GitHub allows unverified emails) |
| First Login Flow | `first broker login` |

3. Click **Save**

---

## First Login Behavior

When a user logs in via a social provider for the first time, Keycloak's default `first broker login` flow handles it:

1. **Review profile** - user confirms/edits username and email
2. **Create user** - a Keycloak user is auto-provisioned
3. **Link account** - if a local account with the same email exists, prompts the user to link

No custom authentication flow is needed.

---

## Verify the setup

1. Open the Keycloak account page via your proxy (e.g. `https://auth.giretra.com/realms/giretra/account`) in an incognito window
2. You should see login buttons for Google, Facebook, and GitHub below the standard login form
3. Click each one to test the OAuth flow end to end

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "redirect_uri_mismatch" from Google | Check both redirect URIs are registered in Google Console (local + production) |
| Facebook login fails for non-test users | Switch the Facebook app from Development to Live mode |
| GitHub secret lost | Generate a new client secret from GitHub Developer Settings (old one is invalidated) |
| "Invalid parameter: redirect_uri" from Facebook | Ensure both redirect URIs are listed in Facebook Login Settings |
| Keycloak shows "Identity provider not found" | Verify the alias matches (`google`, `facebook`, `github`) and the realm is `giretra` |
| Works locally but not in production | Verify production redirect URIs are added to all providers and Keycloak `KC_HOSTNAME` is set to `auth.giretra.com` |
