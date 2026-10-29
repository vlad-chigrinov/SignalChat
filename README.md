# SignalChat

Вы описали проект с онлайн-чатом, комнатами и личными сообщениями, а также входом через Google, с бэкендом на ASP.NET Core и фронтендом на Vue 3. Такой стек не входит в стандартные варианты, поэтому выбран "other" для создания структуры с dotnet 10 и Vue 3, чтобы обеспечить нужную функциональность и локальную разработку.

## Stack

- Backend: ASP.NET Core (.NET 10), EF Core + PostgreSQL 17, SignalR (real-time).
- Frontend: Vue 3 + Vite.

## Run (one command)

Prerequisites: .NET SDK 10, Docker, Node.js.

```powershell
./scripts/dev.ps1      # Windows (PowerShell)
```

```sh
./scripts/dev.sh       # macOS / Linux / Git Bash
```

It creates `.env` from `.env.example` on first run (random DB password), trusts the
HTTPS dev certificate, starts PostgreSQL in Docker (`127.0.0.1:5433`), the frontend
dev server when `frontend/` exists, and the API in the foreground. Ctrl+C stops the
API and the frontend; `docker compose down` stops the database.

| What | URL |
|------|-----|
| Frontend | http://localhost:5173 |
| API | https://localhost:5001 (http://localhost:5000 redirects) |
| Liveness | https://localhost:5001/health |
| Readiness (DB) | https://localhost:5001/health/ready |
| SignalR hub | wss://localhost:5001/hubs/chat |

## Backend

```
backend/
  SignalChat.slnx
  src/SignalChat.Api             # host: endpoints, SignalR hubs, config
  src/SignalChat.Domain          # entities and rules, no dependencies
  src/SignalChat.Infrastructure  # EF Core DbContext, migrations, PostgreSQL
  tests/SignalChat.Api.Tests     # integration tests (WebApplicationFactory)
```

- Configuration: the API reads the repository-root `.env` (same `POSTGRES_*`
  variables as docker-compose). Real environment variables win. Never commit `.env`.
- `AllowedHosts` (host allowlist, `;`-separated): the base config allows only `localhost`,
  Development adds `127.0.0.1`. In production set the `AllowedHosts` environment variable to the API host.
- Migrations are applied on startup in Development. To add one:
  `cd backend && dotnet tool restore && dotnet ef migrations add <Name> --project src/SignalChat.Infrastructure --startup-project src/SignalChat.Api --output-dir Persistence/Migrations`
- Tests: `cd backend && dotnet test` (SQLite in memory and a fake Google: no Docker, no keys needed)
- Realtime chat (SignalR hub `/hubs/chat`, presence, typing): contract for the frontend in [docs/realtime-hub.md](docs/realtime-hub.md).

## Google sign-in

The app runs without Google credentials; only `/auth/login` answers 503 until you add them.

1. Open [Google Cloud Console](https://console.cloud.google.com/) → create or pick a project.
2. **APIs & Services → OAuth consent screen**: type *External*, app name, support email.
   Scopes: `openid`, `email`, `profile`. While in *Testing*, add your Google account under *Test users*.
3. **APIs & Services → Credentials → Create credentials → OAuth client ID**, type *Web application*.
   - **Authorized redirect URI**: `https://localhost:5001/auth/callback/google`
   - Authorized JavaScript origins: not needed (the backend runs the whole flow).
4. Put the values in `.env` at the repository root (never in git):

   ```
   GOOGLE_CLIENT_ID=<...>.apps.googleusercontent.com
   GOOGLE_CLIENT_SECRET=<...>
   ```

5. Restart `scripts/dev.*`, open http://localhost:5173 and sign in.

In production, register `https://<your-api-host>/auth/callback/google` the same way.

### How auth works (contract for the frontend)

| Endpoint | What it does |
|----------|--------------|
| `GET /auth/login?returnUrl=/rooms` | Full-page navigation (not `fetch`). Google OIDC, Authorization Code + PKCE, redirect URI built by the backend. Ends on `FRONTEND_URL` + `returnUrl` (local paths only), or on `/?authError=google` on failure. |
| `GET /auth/me` | `200 { id, displayName, avatarUrl, email }` or `401`. |
| `POST /auth/logout` | `204`, session cookie cleared. |

- Session = cookie `__Host-signalchat.session`: `HttpOnly`, `Secure`, `SameSite=Strict`, 14 days sliding.
  It holds only the internal user id; Google tokens are never stored or sent to the browser.
- The same cookie authenticates SignalR (`/hubs/chat`): no access token in the URL.
- CSRF: `SameSite=Strict` plus an `Origin` check. `POST`/`PUT`/`PATCH`/`DELETE` and WebSocket
  handshakes from an origin outside `CORS_ALLOWED_ORIGINS` get `403`. The SPA sends no CSRF token;
  calls must go through the same origin (the Vite proxy in dev) with `credentials: 'include'`.
- Email is encrypted in the database (AES-256-GCM, `PII_ENCRYPTION_KEY`). Display name and avatar
  are the public chat profile and are stored as is.

## Working on it

This project was started from AgentsRoom. The architecture decisions and the
security rules the agents must follow are in AGENTS.md.

The code is yours and it is local. Nothing is hosted by AgentsRoom: push it to
the git host you prefer, deploy it where you prefer.

## Frontend

```sh
cd frontend && npm install && npm run dev
```

Dev server: http://localhost:5173. It proxies `/api`, `/auth`, `/hubs` (WebSocket)
to the backend over HTTPS (`BACKEND_URL` in `frontend/.env`, default
`https://localhost:5001`). Details: [frontend/README.md](frontend/README.md).
