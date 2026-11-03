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
- A direct conversation has a status: `accepted` (a chat) or `pending` (a request not yet answered). Only accepted conversations are active: messages, `JoinRoom`, `Typing` and presence use them. See [Direct messages by consent](#direct-messages-by-consent).
- Sending is only through the hub. There is no REST endpoint for sending messages. When the hub is not connected, the UI must block sending.

## Client to server methods

| Method | Arguments | Returns | Rules |
|---|---|---|---|
| `JoinRoom` | `conversationId: string` | nothing | Accepts a room id, or a direct conversation id in status `accepted`. A pending direct conversation gives `not_found`. Membership is checked in the database. Idempotent. Other members get `UserJoined`. |
| `LeaveRoom` | `conversationId: string` | nothing | Leaves the group and notifies the others with `UserLeft`. A no-op when the connection had not joined. |
| `SendMessage` | `conversationId: string`, `text: string` | `Message` | Membership is checked on every call. Order: rate limit, then membership, then save to the database, then broadcast `MessageReceived` to the whole group (the sender included). Returns the saved message as the acknowledgement. In a pending direct conversation the initiator gets `dm_not_accepted`, and the recipient gets `not_found`. |
| `Typing` | `conversationId: string` | nothing | Only for conversations this connection joined, and the membership is checked in the database again, so a former member gets `not_found`. Repeated signals inside the throttle window are dropped silently. Others get `UserTyping`. |

Text is trimmed. Empty text or text longer than 2000 characters is `invalid_message`.

## Server to client events

| Event | Payload |
|---|---|
| `MessageReceived` | `Message` (see below) |
| `UserJoined` / `UserLeft` | `{ conversationId, user: UserSummary }` |
| `UserTyping` | `{ conversationId, userId }` |
| `PresenceChanged` | `{ userId, isOnline }` |
| `DirectRequestReceived` | `{ conversationId, from: UserSummary, text, createdAt }`. Sent only to the recipient (their `user:` group) when a request is created or reopened. |
| `DirectRequestAccepted` | `{ conversationId, peer: UserSummary }`. Sent only to the initiator (their `user:` group) when the recipient accepts. A decline sends no event. |
| `RoomMemberJoined` | `{ roomId, user: UserSummary, joinedAt }`. Someone became a member of a room, by `POST /api/rooms/{id}/join` or by an invitation. Sent once, to the room group, so only the connections that have the room open (called `JoinRoom`) get it, the inviter included. The joiner's own connections are not in the group yet and get nothing. A join of someone who is a member already sends nothing. Not the same as `UserJoined`, which only says that a connection opened the group. |
| `AddedToRoom` | `{ room: Room }` with the room as `GET /api/rooms` lists it (`kind`, `id`, `name`, `description`, `memberCount`, `lastMessage`). Sent to the invitee's `user:` group (every tab) when a member invites them. The client lists the room at once, loads the presence snapshot again, and calls `JoinRoom` when the user opens the room. |

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

```json
{ "conversationId": "3f2a1b4c-5d6e-4f70-8192-a3b4c5d6e7f8", "from": { "id": "7a659d8e-7310-4214-9547-3abd23778a18", "displayName": "Anna", "avatarUrl": null }, "text": "Hi, can we talk?", "createdAt": "2026-10-29T10:15:30.123+00:00" }
```

```json
{ "conversationId": "3f2a1b4c-5d6e-4f70-8192-a3b4c5d6e7f8", "peer": { "id": "7a659d8e-7310-4214-9547-3abd23778a18", "displayName": "Anna", "avatarUrl": null } }
```

```json
{ "roomId": "3f2a1b4c-5d6e-4f70-8192-a3b4c5d6e7f8", "user": { "id": "7a659d8e-7310-4214-9547-3abd23778a18", "displayName": "Anna", "avatarUrl": null }, "joinedAt": "2026-11-02T10:15:30.123+00:00" }
```

```json
{ "room": { "kind": "room", "id": "3f2a1b4c-5d6e-4f70-8192-a3b4c5d6e7f8", "name": "Dev", "description": "Code", "memberCount": 3, "lastMessage": null } }
```

`UserSummary` never carries an email. Email exists only in `GET /auth/me`.

### `UserProfileChanged`

```json
{ "id": "7a659d8e-7310-4214-9547-3abd23778a18", "displayName": "Anna Smirnova", "avatarUrl": "/api/users/7a659d8e-7310-4214-9547-3abd23778a18/avatar?v=2" }
```

Sent when a user changes their name or avatar (`PUT /api/me/display-name`, `PUT` or `DELETE /api/me/avatar`). The audience is
the user's room mates, the peers of their accepted direct conversations, and the user's own connections (other tabs). It is sent
once, after the change is stored; a refused change sends nothing. Pending requests are not part of the audience (as with
presence): a request's sender shows the new profile after the next `GET /api/direct-requests/incoming`.

The event is a hint, not the source of truth: every REST list returns the current profile, so a client that misses it (offline,
reconnecting) is correct again after the next load. Why an event and not only "on next load": a name that stays old until a
reload looks like a bug in a chat, and the event costs one group send.

## Presence

- Presence is scoped. A user sees and receives presence only for users who share a room or a direct conversation with them.
- A user is online while at least one connection is open. Several tabs produce one `PresenceChanged(true)` on the first connection.
- `PresenceChanged(false)` is sent only after the last connection has been closed for 5 s (the offline grace). A reconnect inside that window produces no event at all. The snapshot counts a user inside the grace window as online.
- Snapshot: `GET /api/presence/online` returns `UserId[]`, only the online peers that share a conversation with the caller. Use it after load or reconnect.
- Only accepted direct conversations count as shared. A pending request does not make two users peers for presence.
- A new room member and the old ones become peers at that moment, and the snapshot they loaded earlier does not list each other. So after a join or an invitation the server sends `PresenceChanged(newMember, isOnline)` to the personal group of every other member (the right value in both directions, also when it clears a stale "online"), and the new member reloads the snapshot (the client does it after a REST join and on `AddedToRoom`). Later changes of the new member reach the old ones through the usual `PresenceChanged`, because peers are read from the database each time.

## Groups (server side only)

- `room:{id}` for rooms, `dm:{id}` for direct conversations, `user:{userId}` for presence. Every connection joins its `user:` group on connect.
- Leaving a room over REST (`POST /api/rooms/{id}/leave`) also removes the user's open connections from its group and forgets the room on each of them. The remaining members get `UserLeft`; the leaver does not.
- After a REST join the client must call `JoinRoom` again on the same connection (no reconnect needed). `JoinRoom` is idempotent, and it joins the group again only because the earlier leave forgot the room.

## Room members and invitations

- **Member list:** `GET /api/rooms/{id}/members` answers `200` with `[{ user: UserSummary, role: "owner" | "member" }]`, the owner first, then by name, at most 500. Only a member may ask: anyone else, and a missing room, get the same `404 not_found`. It carries the current name and avatar and never an email. Presence is not in the list: the client combines it with the presence snapshot.
- **Invite:** `POST /api/rooms/{id}/invitations` with `{ "userId" }` answers `204`; the invitee is a member at once, with no confirmation. Rules, all on the server: the caller must be a member of the room; the invitee must be a chat partner of the caller (an **accepted** direct conversation; a waiting or declined request does not count). Everything else is `404 not_found`, the same answer for an unknown id, a stranger, the caller themselves, a room that is not theirs and a request that was never accepted, so the route cannot be used to probe users or rooms. A user who is in the room already gets `409 already_member` and nothing changes (no duplicate row). The route is rate limited (`RoomInvitePerMinute`), refused invitations included.
- **Events after a stored change:** the room group gets `RoomMemberJoined`, the invitee gets `AddedToRoom`, every other member's personal group gets the newcomer's `PresenceChanged`. A refused or repeated request sends nothing.
- **"Joined" in the UI:** the notice is a client-side line shown among the messages by time. It is not a message: it is not stored, not in the history pages, and does not move a cursor.

## Direct messages by consent

A direct conversation starts as a request. Nobody can write to a user who has not answered.

- **Send a request:** `POST /api/direct-conversations` with `{ "userId", "text" }`. `text` is the request's first message: required for a new request (`invalid_message` otherwise), trimmed, at most 2000 characters. Answers `201` with the conversation when the request is created or reopened, and `200` with the existing accepted conversation when there is one. The recipient gets `DirectRequestReceived` at once.
- **Recipient's inbox:** `GET /api/direct-requests/incoming` lists the requests waiting for an answer, as `{ id, from, text, createdAt }`. `id` is the conversation id to accept or decline.
- **Accept:** `POST /api/direct-conversations/{id}/accept` answers `204`. The initiator gets `DirectRequestAccepted` and calls `JoinRoom` for the conversation (no reconnect needed).
- **Decline:** `POST /api/direct-conversations/{id}/decline` answers `204` and is silent. The initiator gets no event and keeps seeing the conversation as `pending`.
- Only the recipient of a pending request can accept or decline it. For anyone else, and for a conversation that is not pending, the answer is `not_found`.
- **Reopen:** a user who declined a request can write to the initiator first. That creates a new pending request with the decliner as its initiator (`201`), and the original initiator now sees the incoming request.
- **Status in `GET /api/direct-conversations`:** `accepted` or `pending`. A declined request is shown to its initiator as `pending`, never as declined. The recipient does not see it at all until the reopen.
- A user may have at most 20 outgoing pending requests (`dm_request_limit_reached`). The create route is rate limited (see [REST rate limits](#rest-rate-limits)).
- Sending to a pending conversation is refused: the initiator gets `dm_not_accepted`, the recipient gets `not_found`.

## Error codes

Hub errors reach the client as `HubException`. Because SignalR does not expose details, the stable code is the text after the last `": "` in the message, for example `... on the server. HubException: not_found`. Match the code, not the whole message.

REST errors carry the same codes in ProblemDetails `code`.

| Code | Meaning |
|---|---|
| `not_found` | The conversation does not exist, or the caller is not a member, or the id is malformed, or the conversation is pending and the caller may not act on it. |
| `invalid_message` | Empty after trimming, or longer than 2000 characters. A new direct request without text is also `invalid_message`. |
| `rate_limited` | More than 20 messages inside a 10-second sliding window, or more than 30 `JoinRoom`/`LeaveRoom` calls inside 10 s. Over REST the same code comes with HTTP 429 and a `Retry-After` header. |
| `too_many_groups` | More than 100 conversations joined on one connection. |
| `unauthenticated` | No valid user identity on the connection. |
| `dm_request_pending` | (409) The caller already has a pending request to this user, or repeats one after a decline. |
| `dm_incoming_request_exists` | (409) The target user has an unanswered request to the caller. Answer it first (accept or decline). |
| `dm_request_limit_reached` | (409) The caller already has 20 outgoing pending requests. |
| `dm_not_accepted` | (409) A message to a pending direct conversation, sent by its initiator. |
| `already_member` | (409) The invited user is in the room already. |

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
| `POST /api/rooms/{id}/invitations` | 20 | `RoomInvitePerMinute` |
| `POST /api/direct-conversations` | 10 | `DirectConversationCreatePerMinute` |
| `POST /api/rooms` | 10 | `RoomCreatePerMinute` |
| `PUT /api/me/display-name`, `PUT` and `DELETE /api/me/avatar` | 10, shared | `ProfileUpdatePerMinute` |

The numbers above are defaults. Override them under `Chat:RateLimits` in configuration (for example `Chat:RateLimits:RoomCreatePerMinute`).

## Reconnect rules (client)

Groups belong to a connection, so after `onreconnected` the client must:

1. Call `JoinRoom` again for every open conversation.
2. Reload the latest REST history page of each conversation and merge it. Merging must be idempotent by message id.
3. Reload the presence snapshot from `GET /api/presence/online`.

While disconnected, sending is disabled.

## E2E test sign-in

- `POST /auth/test-login` (body `{"handle": "alice"}`) exists only when `E2E_TEST_AUTH=true` and the environment is `Testing` or `E2E`. With the flag on in `Development` or `Production` the host refuses to start.
- The E2E backend runs with `ASPNETCORE_ENVIRONMENT=E2E` (`scripts/e2e-backend.ps1` or `scripts/e2e-backend.sh`), against the separate `signalchat_e2e` database, recreated on every run.
- The route is limited per client address: 20 requests per minute (`Chat:RateLimits:TestLoginPerMinute`). Past the limit it answers `429` with `Retry-After` and `rate_limited`.
- The host logs a warning at startup while the flag is on.

## Scaling

`AddChatSignalR()` is the single registration point. A Redis backplane (`AddStackExchangeRedis`) belongs there behind configuration. It is not enabled. The presence tracker, the rate limiter and the connection registry are interfaces with in-memory implementations, so they can move to a shared store later.
