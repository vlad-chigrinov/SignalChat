/**
 * Wire types shared with the backend REST API (see ./CONTRACT.md).
 * Dates are ISO 8601 strings in UTC.
 */

export type UserId = string
export type ConversationId = string
export type MessageId = string

export interface UserSummary {
  id: UserId
  displayName: string
  avatarUrl: string | null
}

export interface CurrentUser extends UserSummary {
  email: string
}

export interface MessagePreview {
  authorId: UserId
  text: string
  sentAt: string
  /** Photos of the message; 0 for a text. `text` is then empty or the caption. */
  photoCount: number
}

export interface Room {
  kind: 'room'
  id: ConversationId
  name: string
  description: string
  memberCount: number
  lastMessage: MessagePreview | null
}

/**
 * `accepted`: a chat both sides can write in. `pending`: the caller's own request, not answered
 * yet (the recipient never sees it in this list).
 */
export type DirectStatus = 'accepted' | 'pending'

export interface DirectConversation {
  kind: 'direct'
  id: ConversationId
  peer: UserSummary
  status: DirectStatus
  lastMessage: MessagePreview | null
}

/** A request addressed to the caller, waiting for accept or decline. `id` is the conversation id. */
export interface IncomingDirectRequest {
  id: ConversationId
  from: UserSummary
  text: string
  createdAt: string
}

export type Conversation = Room | DirectConversation

/**
 * One photo of a message. `url` and `thumbnailUrl` are paths on the API that need the session (a photo is never public);
 * show them through `attachmentSrc`. The sizes let the feed reserve the space before the picture has loaded.
 */
export interface Attachment {
  id: string
  width: number
  height: number
  thumbnailWidth: number
  thumbnailHeight: number
  sizeBytes: number
  contentType: string
  fileName: string
  url: string
  thumbnailUrl: string
}

/**
 * One emoji under a message: how many people chose it, and whether the caller is one of them.
 * Never says who the others are.
 */
export interface Reaction {
  emoji: string
  count: number
  reactedByMe: boolean
}

/** How many people chose an emoji, as the live event carries it (the same for every member). */
export interface ReactionCount {
  emoji: string
  count: number
}

/** `owner` created the room. The list shows the current public profile of the member, never an email. */
export type RoomRole = 'owner' | 'member'

export interface RoomMember {
  user: UserSummary
  role: RoomRole
}

export interface Message {
  id: MessageId
  conversationId: ConversationId
  author: UserSummary
  /** The text, or the caption of a photo message (may be empty then). */
  text: string
  sentAt: string
  /** Empty for a text message; 1..9 photos otherwise. */
  attachments: Attachment[]
  /** Counts per emoji in catalog order; empty when nobody reacted. */
  reactions: Reaction[]
}

/** One page of history. `items` are chronological (oldest first). */
export interface CursorPage<T> {
  items: T[]
  /** Pass as `before` to get the previous (older) page; null when there is none. */
  nextCursor: string | null
}

export interface HistoryQuery {
  before: string | null
  limit: number
}

export interface PresenceChange {
  userId: UserId
  isOnline: boolean
}

export type LoginOutcome = 'redirected' | 'signed-in'

/**
 * Stable `code` of a ProblemDetails answer (see CONTRACT.md). The UI maps codes to text,
 * never the server's own message.
 */
export type ApiErrorCode =
  | 'not_found'
  | 'invalid_cursor'
  | 'invalid_limit'
  | 'query_too_short'
  | 'invalid_room_name'
  | 'invalid_room_description'
  | 'room_name_taken'
  | 'room_limit_reached'
  | 'owner_cannot_leave'
  | 'self_conversation'
  | 'invalid_message'
  | 'dm_request_pending'
  | 'dm_incoming_request_exists'
  | 'dm_request_limit_reached'
  | 'already_member'
  | 'invalid_display_name'
  | 'invalid_avatar'
  | 'avatar_too_large'
  | 'invalid_attachments'
  | 'invalid_image'
  | 'attachment_too_large'
  | 'storage_unavailable'
  | 'invalid_reaction'
  | 'dm_not_accepted'
  | 'rate_limited'

export interface CreateRoomInput {
  name: string
  description: string
}
