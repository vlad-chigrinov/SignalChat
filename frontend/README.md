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
`e2e/realtime.fixme.spec.ts` is a skipped two-browser-context skeleton for the SignalR ticket (#7).
Reports and traces (`playwright-report/`, `test-results/`) are git-ignored.

## Backend proxy

The dev server forwards `/api`, `/auth` and `/hubs` (WebSocket enabled, for SignalR)
to the backend over HTTPS. Default target: `https://localhost:5001`. To change it,
copy `.env.example` to `.env` and set `BACKEND_URL`. The `.env` file is git-ignored.

## Routes

| Path        | View             |
| ----------- | ---------------- |
| `/login`    | Google sign-in   |
| `/`         | Chat list        |
| `/chat/:id` | Single chat      |

Auth tokens never go to `localStorage`: the backend sets them as secure cookies.
