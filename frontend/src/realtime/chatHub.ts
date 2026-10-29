import type { ConversationId, Message } from '@/api'

import type { ConnectionState, HubErrorCode, HubEventMap, HubEventName } from './hubContract'

export type Unsubscribe = () => void

/** Thrown by every hub call. `code` is one stable string the UI maps to text. */
export class HubCallError extends Error {
  readonly code: HubErrorCode

  constructor(code: HubErrorCode) {
    super(`Hub call failed: ${code}`)
    this.name = 'HubCallError'
    this.code = code
  }
}

/** Transport-agnostic view of the realtime hub. Implemented by SignalR and by an in-memory fake. */
export interface ChatHub {
  start(): Promise<void>
  stop(): Promise<void>
  /** Current connection state, read synchronously before choosing a transport. */
  currentState(): ConnectionState

  joinRoom(conversationId: ConversationId): Promise<void>
  leaveRoom(conversationId: ConversationId): Promise<void>
  /** Resolves with the saved message (the ack). Rejects with `HubCallError`. */
  sendMessage(conversationId: ConversationId, text: string): Promise<Message>
  typing(conversationId: ConversationId): Promise<void>

  on<Name extends HubEventName>(
    event: Name,
    handler: (payload: HubEventMap[Name]) => void,
  ): Unsubscribe
  onConnectionState(handler: (state: ConnectionState) => void): Unsubscribe
  /** Fires after an automatic reconnect. Groups are gone by then and must be joined again. */
  onReconnected(handler: () => void): Unsubscribe
}
