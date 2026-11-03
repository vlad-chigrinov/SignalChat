/**
 * Client side of the ChatHub contract (see .agentsroom memory `features/realtime-hub-contract`).
 * Names and payloads must match the backend hub exactly.
 */
import type {
  ConversationId,
  Message,
  MessageId,
  PresenceChange,
  ReactionCount,
  Room,
  UserId,
  UserSummary,
} from '@/api'

export const HUB_PATH = '/hubs/chat'

/** Method names the client calls on the hub. */
export const HUB_METHODS = {
  JoinRoom: 'JoinRoom',
  LeaveRoom: 'LeaveRoom',
  SendMessage: 'SendMessage',
  Typing: 'Typing',
} as const

/** Event names the hub pushes to the client. */
export const HUB_EVENTS = {
  MessageReceived: 'MessageReceived',
  UserJoined: 'UserJoined',
  UserLeft: 'UserLeft',
  UserTyping: 'UserTyping',
  PresenceChanged: 'PresenceChanged',
  DirectRequestReceived: 'DirectRequestReceived',
  DirectRequestAccepted: 'DirectRequestAccepted',
  UserProfileChanged: 'UserProfileChanged',
  ReactionsChanged: 'ReactionsChanged',
  MyReactionChanged: 'MyReactionChanged',
  RoomMemberJoined: 'RoomMemberJoined',
  AddedToRoom: 'AddedToRoom',
} as const

/** The client hides a typing indicator after this long without a new `UserTyping`. */
export const TYPING_INDICATOR_TIMEOUT_MS = 4000
/** Minimum gap between two `Typing` calls for one conversation. The server throttles at 2 s too. */
export const TYPING_SEND_INTERVAL_MS = 2000
/**
 * A keystroke inside the interval is sent once more when the interval has passed, so a short
 * burst still shows. The margin keeps that call outside the server's window too.
 */
export const TYPING_TRAILING_MARGIN_MS = 250
/** Delays before each automatic reconnect attempt. */
export const RECONNECT_DELAYS_MS: readonly number[] = [0, 2000, 5000, 10000, 30000]

/**
 * Codes the hub puts in `HubException`. A non-member always gets `not_found` (C3), never a
 * separate "forbidden". `unknown` is what the client uses for anything else.
 */
export const HUB_ERROR_CODES = [
  'not_found',
  'invalid_message',
  'rate_limited',
  'too_many_groups',
  'unauthenticated',
  'dm_not_accepted',
] as const

export type HubErrorCode = (typeof HUB_ERROR_CODES)[number] | 'unknown'

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting'

export interface UserTypingEvent {
  conversationId: ConversationId
  userId: UserId
}

export interface ConversationMemberEvent {
  conversationId: ConversationId
  user: UserSummary
}

/** Sent to the recipient of a request, when it is created or reopened. */
export interface DirectRequestReceivedEvent {
  conversationId: ConversationId
  from: UserSummary
  text: string
  createdAt: string
}

/** Sent to the writer of a request, when the recipient accepts it. A decline sends nothing. */
export interface DirectRequestAcceptedEvent {
  conversationId: ConversationId
  peer: UserSummary
}

/**
 * The reactions of a message changed. The complete new counts for every member of the conversation
 * (not a delta), and no user: whether one of them is the caller comes from `MyReactionChanged`.
 */
export interface ReactionsChangedEvent {
  conversationId: ConversationId
  messageId: MessageId
  reactions: ReactionCount[]
}

/** Sent to the caller's own tabs only: the emoji they hold on the message now (null: none). */
export interface MyReactionChangedEvent {
  conversationId: ConversationId
  messageId: MessageId
  emoji: string | null
}

/**
 * Someone became a member of a room, by joining or by invitation. Sent once, to the connections that have the
 * room open. Not the same as `UserJoined`, which only says that a connection opened the room's group.
 */
export interface RoomMemberJoinedEvent {
  roomId: ConversationId
  user: UserSummary
  joinedAt: string
}

/** Sent to the invitee's own connections when a member of a room invites them. */
export interface AddedToRoomEvent {
  room: Room
}

/** Payload of each server event the client subscribes to. */
export interface HubEventMap {
  MessageReceived: Message
  UserJoined: ConversationMemberEvent
  UserLeft: ConversationMemberEvent
  UserTyping: UserTypingEvent
  PresenceChanged: PresenceChange
  DirectRequestReceived: DirectRequestReceivedEvent
  DirectRequestAccepted: DirectRequestAcceptedEvent
  /** A user with a shared conversation, or the caller in another tab, changed their name or avatar. */
  UserProfileChanged: UserSummary
  ReactionsChanged: ReactionsChangedEvent
  MyReactionChanged: MyReactionChangedEvent
  RoomMemberJoined: RoomMemberJoinedEvent
  AddedToRoom: AddedToRoomEvent
}

export type HubEventName = keyof HubEventMap

export function isHubErrorCode(value: string): value is Exclude<HubErrorCode, 'unknown'> {
  return (HUB_ERROR_CODES as readonly string[]).includes(value)
}
