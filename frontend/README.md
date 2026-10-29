# SignalChat — frontend

Vue 3 + Vite + TypeScript + Pinia + Vue Router. Real-time: `@microsoft/signalr`.

## Commands

```sh
npm install        # once
npm run dev        # dev server on http://localhost:5173
npm run build      # type-check + production build into dist/
npm run lint       # oxlint + eslint
npm run format     # prettier
```

## E2E tests (Playwright)

```sh
npx playwright install chromium   # once: downloads the browser (not committed)
npm run test:e2e                  # starts its own Vite dev server on :5174
```

Specs live in `e2e/`. They run on the in-memory mock API (no backend, no Google keys):
the server is started with `VITE_USE_MOCK_API=true` and `VITE_MOCK_START_SIGNED_OUT=true`,
so the mock begins without a session and "Войти через Google" signs in locally.
Covered: sign-in → chat list → chat → send → older history on scroll, route guard,
logout, plain-text rendering (no HTML injection), no tokens in web storage.
Reports and traces (`playwright-report/`, `test-results/`) are git-ignored.

### E2E against the real backend

```sh
./scripts/e2e-backend.ps1         # from the repo root, in its own terminal: API on https://localhost:5001
npm run test:e2e:real             # from frontend/: users chat through the real SignalR hub
```

The API runs in the E2E environment (`ASPNETCORE_ENVIRONMENT=E2E`, set by the script): the test
sign-in is enabled there only. `e2e/real/` runs room chat, leave, HTML-as-text, a lost connection
with catch-up, and direct messages by consent: a first message is a request, which the recipient
accepts or declines (a declined request stays "waiting" for its writer). Each user is signed in
through `POST /auth/test-login`. The run stops with a hint when the API is not up or the E2E
environment is not used. Handles and room names are unique per run. Report: `playwright-report-real/`.

## Mock or real API

The app talks to the backend through `src/api`. In the dev server, `VITE_USE_MOCK_API=true`
switches to the in-memory mock; the default is the real backend (`VITE_USE_MOCK_API=false`).
A production build always calls the real API; the mock code itself is still in the bundle.

## Backend proxy

The dev server forwards `/api`, `/auth` and `/hubs` (WebSocket enabled, for SignalR)
to the backend over HTTPS. Default target: `https://localhost:5001`. To change it,
copy `.env.example` to `.env` and set `BACKEND_URL`. The `.env` file is git-ignored.

## Container image

```sh
docker build -t signalchat-web .        # from frontend/: builds the app and the nginx image locally
docker run --rm -p 18080:8080 signalchat-web
```

The image runs nginx as a non-root user (`nginxinc/nginx-unprivileged`) on port 8080 and
serves only `dist/`. In production, Traefik sends `/api`, `/auth`, `/hubs` and `/health` to the
backend and everything else to this container, so the app and the API share one origin.

- `nginx.conf`: the server block (SPA fallback to `index.html`, `/healthz` for the health check,
  `/assets/` cached as immutable, `index.html` revalidated on every load, gzip, `server_tokens off`).
- `nginx-security-headers.conf`: the headers on every response. The Content-Security-Policy is
  the one of `vite.csp.ts` plus `frame-ancestors 'none'` (the meta tag cannot carry it).
  `nginx.csp.spec.ts` fails if the two drift apart. No `Strict-Transport-Security`: Traefik sets it.
- `Referrer-Policy` is `strict-origin-when-cross-origin`: Google avatars load cross-origin and
  keep working, while other sites only see the origin, never a path.

Build the image on a development machine, never on the production host.

## Routes

| Path        | View             |
| ----------- | ---------------- |
| `/login`    | Google sign-in   |
| `/`         | Chat list        |
| `/chat/:id` | Single chat      |

Auth tokens never go to `localStorage`: the backend sets them as secure cookies.
