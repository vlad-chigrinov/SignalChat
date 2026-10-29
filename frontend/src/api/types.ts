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
}

export interface Room {
  kind: 'room'
  id: ConversationId
  name: string
  description: string
  memberCount: number
  lastMessage: MessagePreview | null
}

export interface DirectConversation {
  kind: 'direct'
  id: ConversationId
  peer: UserSummary
  lastMessage: MessagePreview | null
}

export type Conversation = Room | DirectConversation

export interface Message {
  id: MessageId
  conversationId: ConversationId
  author: UserSummary
  text: string
  sentAt: string
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

export interface CreateRoomInput {
  name: string
  description: string
}
