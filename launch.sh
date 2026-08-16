#!/usr/bin/env bash
# Launches Giretra for local/remote testing on a single port:
#  - backend in offline mode (no Keycloak, no PostgreSQL), private on http://127.0.0.1:5067
#  - Angular dev server on http://0.0.0.0:42600, proxying /api and /hubs to the backend
# Open http://<this-machine>:42600 from any device. Ctrl+C stops both.

set -euo pipefail

HOST=0.0.0.0
PORT=42600
BACKEND_URL=http://127.0.0.1:5067

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
FRONTEND_DIR="$SCRIPT_DIR/src/Giretra.Web/ClientApp/giretra-web"

if [ ! -d "$FRONTEND_DIR/node_modules" ]; then
  echo "[launch] Installing frontend dependencies..."
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
  echo "[launch] Shutting down..."
  [ -n "${BACKEND_PID:-}" ] && kill "$BACKEND_PID" 2>/dev/null || true
  [ -n "${FRONTEND_PID:-}" ] && kill "$FRONTEND_PID" 2>/dev/null || true
  wait 2>/dev/null || true
  rm -f "$PROXY_CONF"
}
trap cleanup INT TERM EXIT

echo "[launch] Starting backend (offline mode) on $BACKEND_URL ..."
dotnet run --project "$SCRIPT_DIR/src/Giretra.Web" -- --offline &
BACKEND_PID=$!

echo "[launch] Starting frontend on http://$HOST:$PORT ..."
(cd "$FRONTEND_DIR" && npm run start -- \
  --configuration production \
  --host "$HOST" \
  --port "$PORT" \
  --allowed-hosts \
  --proxy-config "$PROXY_CONF") &
FRONTEND_PID=$!

echo "[launch] Once both are up, open http://<this-machine-ip>:$PORT (any username works in offline mode)"
wait
