#!/usr/bin/env bash
# Runs the whole SignalChat stack locally: PostgreSQL (docker), API (https://localhost:5001)
# and, when present, the Vue frontend (http://localhost:5173). Ctrl+C stops everything.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

PLACEHOLDER="change-me"

# Fills a secret in .env only when it is missing, empty or still the placeholder: an existing
# value is never replaced (a new DB password or PII key would lock out existing data).
ensure_secret() {
  local name="$1" value="$2" current
  current="$(grep -E "^${name}=" .env | head -n 1 | cut -d= -f2- || true)"
  if [[ -n "$current" && "$current" != "$PLACEHOLDER" ]]; then
    return
  fi
  if grep -qE "^${name}=" .env; then
    sed -i.bak "s|^${name}=.*|${name}=${value}|" .env && rm -f .env.bak
  else
    printf '%s=%s\n' "$name" "$value" >> .env
  fi
  echo "Generated ${name} in .env."
}

if [[ ! -f .env ]]; then
  cp .env.example .env
  echo "Created .env from .env.example."
fi
ensure_secret POSTGRES_PASSWORD "$(openssl rand -hex 24)"
ensure_secret PII_ENCRYPTION_KEY "$(openssl rand -base64 32)"

dotnet dev-certs https --check --trust >/dev/null 2>&1 || dotnet dev-certs https --trust

docker compose up -d --wait db

frontend_pid=""
if [[ -f frontend/package.json ]]; then
  [[ -d frontend/node_modules ]] || (cd frontend && npm install)
  (cd frontend && npm run dev) &
  frontend_pid=$!
fi
trap '[[ -n "$frontend_pid" ]] && kill "$frontend_pid" 2>/dev/null || true' EXIT

dotnet run --project backend/src/SignalChat.Api --launch-profile https
