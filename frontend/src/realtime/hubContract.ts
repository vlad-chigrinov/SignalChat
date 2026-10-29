/**
 * Client side of the ChatHub contract (see .agentsroom memory `features/realtime-hub-contract`).
 * Names and payloads must match the backend hub exactly.
 */
import type { ConversationId, Message, PresenceChange, UserId, UserSummary } from '@/api'

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
} as const

/** The client hides a typing indicator after this long without a new `UserTyping`. */
export const TYPING_INDICATOR_TIMEOUT_MS = 4000
/** Minimum gap between two `Typing` calls for one conversation. The server throttles at 2 s too. */
export const TYPING_SEND_INTERVAL_MS = 2000
/** Delays before each automatic reconnect attempt. */
export const RECONNECT_DELAYS_MS: readonly number[] = [0, 2000, 5000, 10000, 30000]

/** Codes the hub puts in `HubException`. `unknown` is what the client uses for anything else. */
export const HUB_ERROR_CODES = [
  'forbidden',
  'not_found',
  'invalid_message',
  'rate_limited',
  'too_many_groups',
  'unauthenticated',
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

/** Payload of each server event the client subscribes to. */
export interface HubEventMap {
  MessageReceived: Message
  UserJoined: ConversationMemberEvent
  UserLeft: ConversationMemberEvent
  UserTyping: UserTypingEvent
  PresenceChanged: PresenceChange
}

export type HubEventName = keyof HubEventMap

export function isHubErrorCode(value: string): value is Exclude<HubErrorCode, 'unknown'> {
  return (HUB_ERROR_CODES as readonly string[]).includes(value)
}
