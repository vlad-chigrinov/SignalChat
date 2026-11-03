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

| Method | Path                                | Answer                                                                                                                                                                                                                                                        |
| ------ | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/rooms`                        | 200 `Room[]`: the rooms the caller is a member of                                                                                                                                                                                                             |
| GET    | `/api/rooms/discover?query=&limit=` | 200 `Room[]`: rooms the caller is NOT a member of. `lastMessage` is always null. `limit` ≤ 50 (client cap).                                                                                                                                                   |
| POST   | `/api/rooms`                        | Body `{ name, description }`. 201 `Room`. 400 `invalid_room_name` (name 3..80 after trim) or `invalid_room_description` (≤ 300). 409 `room_name_taken` (case-insensitive) or `room_limit_reached` (20 rooms owned by the caller).                             |
| POST   | `/api/rooms/{id}/join`              | 204. Idempotent. 404 `not_found`.                                                                                                                                                                                                                             |
| GET    | `/api/rooms/{id}/members`           | 200 `RoomMember[]`: `{ user: UserSummary, role: 'owner' \| 'member' }`, the owner first, at most 500. Members only: 404 `not_found` for anyone else. No email.                                                                                                |
| POST   | `/api/rooms/{id}/invitations`       | Body `{ userId }`. 204: a member adds a chat partner (an accepted direct conversation), with no confirmation. 404 `not_found` for a caller who is not in the room, an unknown user, and a user without an accepted chat (all the same). 409 `already_member`. |
| POST   | `/api/rooms/{id}/leave`             | 204. 404 `not_found` when not a member. 409 `owner_cannot_leave` while the owner has other members. The last member leaving deletes the room.                                                                                                                 |

## Direct conversations

| Method | Path                                     | Answer                                                                                                                                                                                                                                                                                                                                                                                                                |
| ------ | ---------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/direct-conversations`              | 200 `DirectConversation[]`. `status` is `accepted`, or `pending` for the caller's own request that waits for an answer (a declined request is `pending` too, for its writer).                                                                                                                                                                                                                                         |
| POST   | `/api/direct-conversations`              | Body `{ userId, text }`, the first message. 201 `DirectConversation` with `status: pending` when a request is created or reopened. 200 with `status: accepted` for an existing chat (`text` ignored). 400 `invalid_message` (text empty or over 2000, or `self_conversation` for oneself); 404 `not_found` for an unknown user; 409 `dm_request_pending`, `dm_incoming_request_exists` or `dm_request_limit_reached`. |
| GET    | `/api/direct-requests/incoming`          | 200 `IncomingDirectRequest[]`: `{ id, from: UserSummary, text, createdAt }`, the requests waiting for the caller's answer. `id` is the conversation id.                                                                                                                                                                                                                                                               |
| POST   | `/api/direct-conversations/{id}/accept`  | 204. The chat becomes accepted for both sides. 404 `not_found` unless the caller is the recipient of a pending request.                                                                                                                                                                                                                                                                                               |
| POST   | `/api/direct-conversations/{id}/decline` | 204, silent: the writer is not told. 404 `not_found` as for accept.                                                                                                                                                                                                                                                                                                                                                   |

A request is the first message of a direct conversation. Nobody can write to a user who has not
answered it: the writer's messages are refused over the hub until the recipient accepts.
A user may have at most 20 outgoing requests waiting for an answer (`dm_request_limit_reached`).
A declined request stays silent: its writer gets `dm_request_pending`, the same answer as while it
waits. The user who declined may write first later, which opens a new request from them.

Rooms and direct conversations share one id space: `/chat/:id` accepts either.

## Users

| Method | Path                              | Answer                                                                                                                                                                     |
| ------ | --------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | `/api/users/search?query=&limit=` | 200 `UserSummary[]` (`id`, `displayName`, `avatarUrl`). `query` ≥ 2 characters, else 400 `query_too_short`. The caller is never in the result. `limit` ≤ 20. **No email.** |

## Profile (the caller edits only themselves)

No route takes a user id: the user is the one in the session, so nobody can change another person's profile.
All answer 200 with the updated `CurrentUser` (`id`, `displayName`, `avatarUrl`, `email`).

| Method | Path                     | Answer                                                                                                                                                                                                                                                               |
| ------ | ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| PUT    | `/api/me/display-name`   | Body `{ displayName }`. Trimmed; 1..100 characters, no control characters, line separators or bidirectional overrides, else 400 `invalid_display_name`. A chosen name is no longer replaced by Google's on the next sign-in.                                         |
| PUT    | `/api/me/avatar`         | Body is the picture itself (`application/octet-stream`). The type is read from the content: JPEG, PNG, GIF (first frame) or WebP, at most 2 MiB and 4096 px a side. Re-encoded to a 256x256 WebP; metadata is dropped. 400 `invalid_avatar`, 413 `avatar_too_large`. |
| DELETE | `/api/me/avatar`         | Drops the uploaded picture; `avatarUrl` is the Google one again (or `null`).                                                                                                                                                                                         |
| GET    | `/api/users/{id}/avatar` | The stored WebP of any user who uploaded one (any signed-in user may read it); 404 otherwise. Cached for a year: the address carries a version (`?v=N`) that changes on every upload.                                                                                |

`avatarUrl` is either an absolute `https://*.googleusercontent.com/...` address or, for an uploaded avatar, a
**path on the API** (`/api/users/{id}/avatar?v=2`). The client joins such a path to the API origin
(`resolveAvatarSrc`), so with an API on its own origin the page's `img-src` allows that origin.
A profile change reaches the other clients with the SignalR event `UserProfileChanged` (see `docs/realtime-hub.md`).

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

## Reactions

`POST /api/conversations/{id}/messages/{messageId}/reaction`, body `{ "emoji": "👍" }`

One reaction per user and message, held by the server (the key of the table is the pair message + user). The route is a
choice, and the server decides the move: no reaction yet → added; another emoji → replaced; the same emoji again → removed.

- The emoji must be one of the fixed list: 👍 ❤️ 😂 😮 😢 🎉 (`ReactionCatalog` in the backend domain, mirrored by
  `src/api/reactionCatalog.ts`). Anything else is 400 `invalid_reaction`; nothing is stored.
- The caller must be a room member or a side of an accepted direct conversation, and the message must belong to that
  conversation and not be deleted: otherwise 404 `not_found` (a non-member, a missing id and a foreign message look the same).
  The writer of a pending request gets 409 `dm_not_accepted`; its recipient gets 404 (as for the history).
- 200 `{ messageId, reactions: Reaction[] }` — the reactions of the message as the caller sees them.
- Limited to 60 per user per minute: 429 `rate_limited` with `Retry-After`.

`Reaction` is `{ emoji, count, reactedByMe }`: how many people chose the emoji and whether the caller is one of them. It
never says who the others are. Messages carry `reactions: Reaction[]` (catalog order, empty when none) in the history and in
the live `MessageReceived`. Live changes come through the hub events `ReactionsChanged` and `MyReactionChanged` (see
`docs/realtime-hub.md`).

## Presence (expected, owned by #6)

`GET /api/presence/online` → 200 `UserId[]`: only the peers who share a room or a direct
conversation with the caller. Not implemented by the backend yet: the client calls it and
treats a failure as "everyone offline". Live changes come through SignalR `PresenceChanged`.

## Error codes

| Code                         | Meaning                                                                          |
| ---------------------------- | -------------------------------------------------------------------------------- |
| `not_found`                  | Missing, or not a member (never distinguished)                                   |
| `invalid_cursor`             | History cursor is malformed                                                      |
| `invalid_limit`              | `limit` out of range                                                             |
| `query_too_short`            | User search query shorter than 2 characters                                      |
| `invalid_room_name`          | Room name not 3..80 characters after trim                                        |
| `invalid_room_description`   | Room description longer than 300 characters                                      |
| `room_name_taken`            | Another room has the same name (case-insensitive)                                |
| `room_limit_reached`         | The caller already owns 20 rooms                                                 |
| `owner_cannot_leave`         | The owner cannot leave while other members remain                                |
| `self_conversation`          | A direct conversation with oneself                                               |
| `invalid_message`            | Message empty or longer than 2000 (hub, and the first message of a request)      |
| `dm_request_pending`         | The caller's own request to this user waits for an answer (also after a decline) |
| `dm_incoming_request_exists` | This user already wrote to the caller: answer that request first                 |
| `invalid_display_name`       | Profile name empty after trim, over 100 characters, or with forbidden characters |
| `invalid_avatar`             | The upload is not a JPEG, PNG, GIF or WebP, is damaged, or is over 4096 px       |
| `avatar_too_large`           | The upload is over 2 MiB (HTTP 413)                                              |
| `dm_request_limit_reached`   | The caller has 20 requests waiting for an answer                                 |
| `invalid_reaction`           | The reaction is not one of the allowed emoji                                     |
| `dm_not_accepted`            | The writer of a pending request cannot react (REST); the hub uses it for sending |
| `rate_limited`               | Too many requests of a limited route (HTTP 429, with `Retry-After`)              |
| `already_member`             | The invited user is in the room already (HTTP 409)                               |
