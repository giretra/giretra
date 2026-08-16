#!/usr/bin/env bash
# Launches Giretra in "remote" mode on a single port, reachable from the internet:
#  - backend with Keycloak auth (auth.giretra.com) + PostgreSQL, private on http://127.0.0.1:5067
#  - DB host/credentials read from ./.env (Giretra_Db_* variables)
#  - Angular dev server on https://0.0.0.0:42600, proxying /api and /hubs to the backend
#
# Serves HTTPS with a self-signed cert for the public IP: keycloak-js needs a
# secure context (accept the browser warning once). Open https://<public-ip>:42600.
# Override the detected IP with PUBLIC_IP=x.x.x.x ./launch-remote.sh. Ctrl+C stops both.

set -euo pipefail

HOST=0.0.0.0
PORT=42600
BACKEND_URL=http://127.0.0.1:5067

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
FRONTEND_DIR="$SCRIPT_DIR/src/Giretra.Web/ClientApp/giretra-web"
ENV_FILE="$SCRIPT_DIR/.env"
CERT_DIR="$SCRIPT_DIR/.local-certs"

if [ ! -f "$ENV_FILE" ]; then
  echo "[launch-remote] Missing $ENV_FILE (expected Giretra_Db_Host, Giretra_Db_Port, Giretra_Db_Name, Giretra_Db_User, Giretra_Db_Password)" >&2
  exit 1
fi

# Export DB credentials for the backend (ConnectionStringBuilder reads Giretra_Db_*)
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

for var in Giretra_Db_Host Giretra_Db_User Giretra_Db_Password; do
  if [ -z "${!var:-}" ]; then
    echo "[launch-remote] $var is not set in $ENV_FILE" >&2
    exit 1
  fi
done

PUBLIC_IP="${PUBLIC_IP:-$(curl -fsS --max-time 10 https://api.ipify.org)}"
if [ -z "$PUBLIC_IP" ]; then
  echo "[launch-remote] Could not detect public IP; set PUBLIC_IP=x.x.x.x" >&2
  exit 1
fi

# Self-signed cert for the public IP (keycloak-js requires a secure context;
# plain http only works on localhost). One cert file per IP, reused across runs.
SSL_CERT="$CERT_DIR/cert-$PUBLIC_IP.pem"
SSL_KEY="$CERT_DIR/key-$PUBLIC_IP.pem"
if [ ! -f "$SSL_CERT" ] || [ ! -f "$SSL_KEY" ]; then
  echo "[launch-remote] Generating self-signed certificate for $PUBLIC_IP ..."
  mkdir -p "$CERT_DIR"
  openssl req -x509 -newkey rsa:2048 -sha256 -days 365 -nodes \
    -keyout "$SSL_KEY" -out "$SSL_CERT" \
    -subj "/CN=giretra-remote" \
    -addext "subjectAltName=IP:$PUBLIC_IP,IP:127.0.0.1,DNS:localhost" \
    2>/dev/null
fi

if [ ! -d "$FRONTEND_DIR/node_modules" ]; then
  echo "[launch-remote] Installing frontend dependencies..."
  (cd "$FRONTEND_DIR" && npm install)
fi

# Proxy so the browser only ever talks to :42600 (the production build
# configuration uses relative URLs, unlike the development one).
PROXY_CONF="$(mktemp --suffix=.json)"
cat > "$PROXY_CONF" <<EOF
{
  "/api": { "target": "$BACKEND_URL", "changeOrigin": true },
  "/hubs": { "target": "$BACKEND_URL", "changeOrigin": true, "ws": true }
}
EOF

cleanup() {
  trap - INT TERM EXIT
  echo
  echo "[launch-remote] Shutting down..."
  [ -n "${BACKEND_PID:-}" ] && kill "$BACKEND_PID" 2>/dev/null || true
  [ -n "${FRONTEND_PID:-}" ] && kill "$FRONTEND_PID" 2>/dev/null || true
  wait 2>/dev/null || true
  rm -f "$PROXY_CONF"
}
trap cleanup INT TERM EXIT

echo "[launch-remote] Starting backend (Keycloak + PostgreSQL at $Giretra_Db_Host) on $BACKEND_URL ..."
dotnet run --project "$SCRIPT_DIR/src/Giretra.Web" &
BACKEND_PID=$!

echo "[launch-remote] Starting frontend on https://$HOST:$PORT ..."
(cd "$FRONTEND_DIR" && npm run start -- \
  --configuration production \
  --host "$HOST" \
  --port "$PORT" \
  --allowed-hosts \
  --ssl true \
  --ssl-cert "$SSL_CERT" \
  --ssl-key "$SSL_KEY" \
  --proxy-config "$PROXY_CONF") &
FRONTEND_PID=$!

echo "[launch-remote] Once both are up, open https://$PUBLIC_IP:$PORT (accept the self-signed cert warning)"
echo "[launch-remote] Keycloak client giretra-web needs: redirect URI https://$PUBLIC_IP:$PORT/* , post-logout https://$PUBLIC_IP:$PORT/* , web origin https://$PUBLIC_IP:$PORT"
wait
