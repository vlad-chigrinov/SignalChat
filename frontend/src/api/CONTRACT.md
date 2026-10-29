# REST contract used by the frontend

Matches the real backend (`backend/`, ticket #4). Types live in `types.ts`, the HTTP client in
`httpApi.ts`, the mock in `mock/mockApi.ts` (it must behave like the server: same rules, codes
and statuses). Change the backend, this file, the types and the mock together.

## Conventions

- Same origin (Vite dev proxy in development). JSON bodies, camelCase fields.
- Ids are GUIDs. Dates are ISO 8601 strings in UTC.
- Session: HttpOnly + Secure + SameSite cookie set by the backend. The frontend never sees a
  token and stores nothing in `localStorage` / `sessionStorage`.
- Every API call carries `X-Requested-With: XMLHttpRequest`. For those, an unauthenticated
  call answers **401** (not a 302 to Google).
- Access is checked on the server. A conversation the caller does not belong to, a missing
  conversation and a non-GUID id all answer **404** (C3: no way to probe which ids exist).
  There is no 403 on these routes.
- Errors are ProblemDetails JSON with a stable `code` field. The client exposes it as
  `ApiError.code`; the UI maps codes to Russian text (`utils/errorMessage.ts`), never the
  server's own message.

## Auth

| Method | Path                            | Answer                                                                   |
| ------ | ------------------------------- | ------------------------------------------------------------------------ |
| GET    | `/auth/login?returnUrl=/chat/x` | Browser navigation to Google; back to `returnUrl` (relative paths only). |
| GET    | `/auth/me`                      | 200 `CurrentUser` (with `email`) or 401                                  |
| POST   | `/auth/logout`                  | 204, clears the cookie                                                   |

## Rooms

| Method | Path                                | Answer                                                                                                                                                                                                                            |
| ------ | ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/rooms`                        | 200 `Room[]`: the rooms the caller is a member of                                                                                                                                                                                 |
| GET    | `/api/rooms/discover?query=&limit=` | 200 `Room[]`: rooms the caller is NOT a member of. `lastMessage` is always null. `limit` ≤ 50 (client cap).                                                                                                                       |
| POST   | `/api/rooms`                        | Body `{ name, description }`. 201 `Room`. 400 `invalid_room_name` (name 3..80 after trim) or `invalid_room_description` (≤ 300). 409 `room_name_taken` (case-insensitive) or `room_limit_reached` (20 rooms owned by the caller). |
| POST   | `/api/rooms/{id}/join`              | 204. Idempotent. 404 `not_found`.                                                                                                                                                                                                 |
| POST   | `/api/rooms/{id}/leave`             | 204. 404 `not_found` when not a member. 409 `owner_cannot_leave` while the owner has other members. The last member leaving deletes the room.                                                                                     |

## Direct conversations

| Method | Path                        | Answer                                                                                                                                                  |
| ------ | --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/direct-conversations` | 200 `DirectConversation[]`                                                                                                                              |
| POST   | `/api/direct-conversations` | Body `{ userId }`. 200 `DirectConversation`, both for a new and an existing one. 400 `self_conversation`. 404 `not_found` when the user does not exist. |

Rooms and direct conversations share one id space: `/chat/:id` accepts either.

## Users

| Method | Path                              | Answer                                                                                                                                                                     |
| ------ | --------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/users/search?query=&limit=` | 200 `UserSummary[]` (`id`, `displayName`, `avatarUrl`). `query` ≥ 2 characters, else 400 `query_too_short`. The caller is never in the result. `limit` ≤ 20. **No email.** |

## Messages (cursor pagination, history only)

`GET /api/conversations/{id}/messages?limit=30&before={cursor}`

- `limit` defaults to 30, maximum 100. Otherwise 400 `invalid_limit`.
- 200 `CursorPage<Message>`: `items` chronological (oldest first); `nextCursor` is an opaque
  string to pass as `before` for the previous page, or `null` at the start.
- 400 `invalid_cursor` for a bad cursor. 404 `not_found` for a non-member.

**Sending is not over REST.** There is no `POST` for messages. Messages go only through the
SignalR hub `SendMessage` (see `realtime/`). The client also checks the text itself
(1..2000 characters after trim); the hub rejects `invalid_message` for empty or too long text.
The text is plain text; the UI never renders HTML.

## Presence (expected, owned by #6)

`GET /api/presence/online` → 200 `UserId[]`: only the peers who share a room or a direct
conversation with the caller. Not implemented by the backend yet: the client calls it and
treats a failure as "everyone offline". Live changes come through SignalR `PresenceChanged`.

## Error codes

| Code                       | Meaning                                           |
| -------------------------- | ------------------------------------------------- |
| `not_found`                | Missing, or not a member (never distinguished)    |
| `invalid_cursor`           | History cursor is malformed                       |
| `invalid_limit`            | `limit` out of range                              |
| `query_too_short`          | User search query shorter than 2 characters       |
| `invalid_room_name`        | Room name not 3..80 characters after trim         |
| `invalid_room_description` | Room description longer than 300 characters       |
| `room_name_taken`          | Another room has the same name (case-insensitive) |
| `room_limit_reached`       | The caller already owns 20 rooms                  |
| `owner_cannot_leave`       | The owner cannot leave while other members remain |
| `self_conversation`        | A direct conversation with oneself                |
| `invalid_message`          | Message empty or longer than 2000 (hub)           |
