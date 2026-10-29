#!/usr/bin/env bash
# Starts the API for end-to-end runs on https://localhost:5001 with test sign-in enabled (POST /auth/test-login).
# The database is a clean signalchat_e2e, recreated on every run; the development database is never touched.
# Migrations are applied by the API on startup (E2E). Ctrl+C stops the API.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

E2E_DATABASE="signalchat_e2e"

if [[ ! -f .env ]]; then
  echo ".env is missing: run ./scripts/dev.sh once first." >&2
  exit 1
fi
postgres_user="$(grep -E '^POSTGRES_USER=' .env | head -n 1 | cut -d= -f2-)"

dotnet dev-certs https --check --trust >/dev/null 2>&1 || dotnet dev-certs https --trust

docker compose up -d --wait db

# Recreate the e2e database from scratch. Connects to the maintenance database "postgres", never to the dev one.
printf '%s\n' \
  "DROP DATABASE IF EXISTS ${E2E_DATABASE} WITH (FORCE);" \
  "CREATE DATABASE ${E2E_DATABASE};" \
  | docker compose exec -T db psql -v ON_ERROR_STOP=1 -U "$postgres_user" -d postgres

POSTGRES_DB="$E2E_DATABASE" \
ASPNETCORE_ENVIRONMENT=E2E \
E2E_TEST_AUTH=true \
  dotnet run --project backend/src/SignalChat.Api --launch-profile e2e
