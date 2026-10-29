# REST contract expected by the frontend

Draft written by the frontend while the backend API (#4) is not ready. Types live in
`types.ts`; the mock implementation in `mock/mockApi.ts` follows this document.
Change both sides together.

## Conventions

- Same origin (through the Vite dev proxy in development). JSON bodies, camelCase fields.
- Dates: ISO 8601 strings, UTC.
- Session: HttpOnly + Secure + SameSite cookie set by the backend. The frontend never
  sees a token and stores nothing in `localStorage` / `sessionStorage`.
- Every API call carries `X-Requested-With: XMLHttpRequest`. For those requests an
  unauthenticated call must answer **401**, not a 302 to Google.
- Access to a conversation is checked on the server. A non-member gets **404**, the same as
  for a missing conversation: no way to probe which ids exist (C3). No 403 for conversations.

## Auth

| Method | Path                            | Answer                                                                                                             |
| ------ | ------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| GET    | `/auth/login?returnUrl=/chat/x` | Browser navigation. Redirects to Google; afterwards back to `returnUrl` (backend must accept relative paths only). |
| GET    | `/auth/me`                      | 200 `CurrentUser` or 401                                                                                           |
| POST   | `/auth/logout`                  | 204, clears the cookie                                                                                             |

## Conversations

| Method | Path                        | Answer                     |
| ------ | --------------------------- | -------------------------- |
| GET    | `/api/rooms`                | 200 `Room[]`               |
| GET    | `/api/direct-conversations` | 200 `DirectConversation[]` |

Rooms and direct conversations share one id space: `/chat/:id` accepts either.

## Messages (cursor pagination)

`GET /api/conversations/{id}/messages?limit=30&before={cursor}`

- Without `before`: the latest `limit` messages.
- 200 `CursorPage<Message>`: `items` chronological (oldest first), `nextCursor` is an
  opaque string to pass as `before` for the previous, older page, or `null` at the start.

**Sending is not over REST.** Messages go only through the SignalR hub `SendMessage`
(see `frontend/src/realtime/`). While the hub is not connected the UI blocks sending.
The text is plain text; the UI never renders HTML.

## Presence

`GET /api/presence/online` returns 200 `UserId[]`: the snapshot at page load. Changes
after that arrive through SignalR (#7) and are applied with
`usePresenceStore().applyPresenceChanged`.
