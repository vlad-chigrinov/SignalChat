# Realtime chat hub (`/hubs/chat`)

Contract for the frontend. The server is the source of truth; if this file and the code disagree, the code wins and this file is fixed in the same change.

## Connection

- SignalR over WebSocket, JSON protocol, camelCase. Dates are ISO 8601 UTC strings.
- URL: `/hubs/chat` on the same origin (the Vite proxy in development). No token in the URL.
- Auth: the session cookie set by `/auth/login`. The handshake's `Origin` must be on the allowlist; a foreign `Origin` is refused at connect (403). A request without `Origin` is a non-browser client and is accepted.
- Client: `withUrl('/hubs/chat')`, `withAutomaticReconnect()`.
- A connection is refused when the session is revoked (logout, or the user's sessions were revoked elsewhere). The server aborts the live connections on logout, so the client sees `closed` and must return to the login screen.
- A user may hold at most 5 open connections. The sixth is refused at connect: the server aborts that new connection (the client sees `closed`) and the open ones stay.

## Id space and access

- Rooms and direct conversations share one id space (GUIDs). The `kind` field tells them apart (`room` or `direct`).
- A non-member always gets `not_found`, never a "forbidden" answer, so the response reveals nothing about which ids exist.
- Sending is only through the hub. There is no REST endpoint for sending messages. When the hub is not connected, the UI must block sending.

## Client to server methods

| Method | Arguments | Returns | Rules |
|---|---|---|---|
| `JoinRoom` | `conversationId: string` | nothing | Accepts a room or a direct conversation id. Membership is checked in the database. Idempotent. Other members get `UserJoined`. |
| `LeaveRoom` | `conversationId: string` | nothing | Leaves the group and notifies the others with `UserLeft`. A no-op when the connection had not joined. |
| `SendMessage` | `conversationId: string`, `text: string` | `Message` | Membership is checked on every call. Order: rate limit, then membership, then save to the database, then broadcast `MessageReceived` to the whole group (the sender included). Returns the saved message as the acknowledgement. |
| `Typing` | `conversationId: string` | nothing | Only for conversations this connection joined, and the membership is checked in the database again, so a former member gets `not_found`. Repeated signals inside the throttle window are dropped silently. Others get `UserTyping`. |

Text is trimmed. Empty text or text longer than 2000 characters is `invalid_message`.

## Server to client events

| Event | Payload |
|---|---|
| `MessageReceived` | `Message` (see below) |
| `UserJoined` / `UserLeft` | `{ conversationId, user: UserSummary }` |
| `UserTyping` | `{ conversationId, userId }` |
| `PresenceChanged` | `{ userId, isOnline }` |

There is no "stopped typing" event. The client hides the typing hint after its own timeout.

### DTO examples

```json
{
  "id": "0190f5c2-6d1e-7a3b-9c1d-2f4e5a6b7c8d",
  "conversationId": "3f2a1b4c-5d6e-4f70-8192-a3b4c5d6e7f8",
  "author": { "id": "7a659d8e-7310-4214-9547-3abd23778a18", "displayName": "Anna", "avatarUrl": null },
  "text": "Hello room",
  "sentAt": "2026-10-29T10:15:30.123+00:00"
}
```

```json
{ "conversationId": "3f2a1b4c-5d6e-4f70-8192-a3b4c5d6e7f8", "user": { "id": "7a659d8e-7310-4214-9547-3abd23778a18", "displayName": "Anna", "avatarUrl": null } }
```

```json
{ "conversationId": "3f2a1b4c-5d6e-4f70-8192-a3b4c5d6e7f8", "userId": "7a659d8e-7310-4214-9547-3abd23778a18" }
```

```json
{ "userId": "7a659d8e-7310-4214-9547-3abd23778a18", "isOnline": true }
```

`UserSummary` never carries an email. Email exists only in `GET /auth/me`.

## Presence

- Presence is scoped. A user sees and receives presence only for users who share a room or a direct conversation with them.
- A user is online while at least one connection is open. Several tabs produce one `PresenceChanged(true)` on the first connection.
- `PresenceChanged(false)` is sent only after the last connection has been closed for 5 s (the offline grace). A reconnect inside that window produces no event at all. The snapshot counts a user inside the grace window as online.
- Snapshot: `GET /api/presence/online` returns `UserId[]`, only the online peers that share a conversation with the caller. Use it after load or reconnect.

## Groups (server side only)

- `room:{id}` for rooms, `dm:{id}` for direct conversations, `user:{userId}` for presence. Every connection joins its `user:` group on connect.
- Leaving a room over REST (`POST /api/rooms/{id}/leave`) also removes the user's open connections from its group and forgets the room on each of them. The remaining members get `UserLeft`; the leaver does not.
- After a REST join the client must call `JoinRoom` again on the same connection (no reconnect needed). `JoinRoom` is idempotent, and it joins the group again only because the earlier leave forgot the room.

## Error codes

Hub errors reach the client as `HubException`. Because SignalR does not expose details, the stable code is the text after the last `": "` in the message, for example `... on the server. HubException: not_found`. Match the code, not the whole message.

| Code | Meaning |
|---|---|
| `not_found` | The conversation does not exist, or the caller is not a member, or the id is malformed. |
| `invalid_message` | Empty after trimming, or longer than 2000 characters. |
| `rate_limited` | More than 20 messages inside a 10-second sliding window, or more than 30 `JoinRoom`/`LeaveRoom` calls inside 10 s. Over REST the same code comes with HTTP 429 and a `Retry-After` header. |
| `too_many_groups` | More than 100 conversations joined on one connection. |
| `unauthenticated` | No valid user identity on the connection. |

## Limits

| Limit | Value |
|---|---|
| Message text | 2000 characters (`Message.MaxTextLength`) |
| Send rate | 20 messages per 10 s sliding window, per user |
| `JoinRoom` + `LeaveRoom` rate | 30 calls per 10 s, per user, shared by all of the user's connections |
| Joined conversations per connection | 100 |
| Typing signal | at most one per 2 s per user and conversation |
| Open connections per user | 5. The sixth is refused at connect; the open ones stay |
| Offline grace (presence) | 5 s after the last connection closes |
| Maximum incoming message size | 32 KB |

### REST rate limits

Per user, fixed window of one minute. The key is the session user; an anonymous request is counted by client IP. Past the limit the answer is `429 Too Many Requests` with a `Retry-After` header (whole seconds) and ProblemDetails whose `code` is `rate_limited`. Each row keeps its own counter.

| Route | Limit per minute | Option |
|---|---|---|
| `GET /api/users/search` | 30 | `UserSearchPerMinute` |
| `GET /api/rooms/discover` | 30 | `RoomDiscoverPerMinute` |
| `POST /api/rooms/{id}/join` and `/leave` | 30, shared | `RoomMembershipPerMinute` |
| `POST /api/direct-conversations` | 10 | `DirectConversationCreatePerMinute` |
| `POST /api/rooms` | 10 | `RoomCreatePerMinute` |

The numbers above are defaults. Override them under `Chat:RateLimits` in configuration (for example `Chat:RateLimits:RoomCreatePerMinute`).

## Reconnect rules (client)

Groups belong to a connection, so after `onreconnected` the client must:

1. Call `JoinRoom` again for every open conversation.
2. Reload the latest REST history page of each conversation and merge it. Merging must be idempotent by message id.
3. Reload the presence snapshot from `GET /api/presence/online`.

While disconnected, sending is disabled.

## Scaling

`AddChatSignalR()` is the single registration point. A Redis backplane (`AddStackExchangeRedis`) belongs there behind configuration. It is not enabled. The presence tracker, the rate limiter and the connection registry are interfaces with in-memory implementations, so they can move to a shared store later.
