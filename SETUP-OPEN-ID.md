# Setup OpenID Connect - Social Login Providers

> Step-by-step guide to create OAuth apps on Google, Facebook, and GitHub, then configure them as Keycloak identity providers for Giretra.

---

## Redirect URIs

All three providers need a redirect URI pointing to your Keycloak instance. Use these values during setup:

| Provider | Redirect URI |
|----------|-------------|
| Google   | `http://localhost:8080/realms/giretra/broker/google/endpoint` |
| Facebook | `http://localhost:8080/realms/giretra/broker/facebook/endpoint` |
| GitHub   | `http://localhost:8080/realms/giretra/broker/github/endpoint` |

> For production, replace `localhost:8080` with your Keycloak domain (e.g. `auth.giretra.mg`).

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
   - Authorized domains: `localhost` (add your production domain later)
   - Scopes: add `email`, `profile`, `openid`
   - Save and continue through test users
5. Back on Credentials, click **Create Credentials** > **OAuth client ID**
6. Application type: **Web application**
7. Name: `Giretra`
8. Authorized redirect URIs: add `http://localhost:8080/realms/giretra/broker/google/endpoint`
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
4. Under **Valid OAuth Redirect URIs**, add: `http://localhost:8080/realms/giretra/broker/facebook/endpoint`
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
   - Application name: `Giretra`
   - Homepage URL: `http://localhost:4200`
   - Authorization callback URL: `http://localhost:8080/realms/giretra/broker/github/endpoint`
4. Click **Register application**
5. Copy the **Client ID**
6. Click **Generate a new client secret** and copy it immediately (shown only once)

### Save to `.env`

```env
GITHUB_CLIENT_ID=Iv1.xxxxxxxxxxxx
GITHUB_CLIENT_SECRET=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

---

## Configure Keycloak

After filling in your `.env` and starting the containers (`docker compose up -d`), configure each provider in Keycloak.

### Access the admin console

1. Open `http://localhost:8080/admin`
2. Log in with `admin` / your `KEYCLOAK_ADMIN_PASSWORD`
3. Select the `giretra` realm (create it first if using manual setup instead of realm import)

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

1. Open `http://localhost:8080/realms/giretra/account` in an incognito window
2. You should see login buttons for Google, Facebook, and GitHub below the standard login form
3. Click each one to test the OAuth flow end to end

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "redirect_uri_mismatch" from Google | Check the redirect URI in Google Console matches exactly: `http://localhost:8080/realms/giretra/broker/google/endpoint` |
| Facebook login fails for non-test users | Switch the Facebook app from Development to Live mode |
| GitHub secret lost | Generate a new client secret from GitHub Developer Settings (old one is invalidated) |
| "Invalid parameter: redirect_uri" from Facebook | Ensure the Valid OAuth Redirect URI in Facebook Login Settings matches exactly |
| Keycloak shows "Identity provider not found" | Verify the alias matches (`google`, `facebook`, `github`) and the realm is `giretra` |
