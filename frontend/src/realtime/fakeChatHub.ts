import type { ConversationId, Message, PresenceChange, UserSummary } from '@/api'
import { sharedMockHistories } from '@/api/mock/mockHistories'

import { HubCallError, type ChatHub, type Unsubscribe } from './chatHub'
import {
  type ConnectionState,
  type ConversationMemberEvent,
  type HubEventMap,
  type HubEventName,
  type UserTypingEvent,
} from './hubContract'

type Listener<Name extends HubEventName> = (payload: HubEventMap[Name]) => void

export interface FakeChatHubOptions {
  /** Author of messages sent through the fake. Null means signed out (calls are rejected). */
  currentUser: () => UserSummary | null
}

/**
 * In-memory hub for unit tests and mock mode. It follows the same contract as the server:
 * a call needs a connection and a known conversation (else `not_found`), a sent message is
 * stored in the shared mock history and echoed to every listener (sender included).
 * The `emit*`, `simulate*` helpers play the server's part in tests.
 */
export class FakeChatHub implements ChatHub {
  private state: ConnectionState = 'disconnected'
  private readonly joined = new Set<ConversationId>()
  private readonly stateListeners = new Set<(state: ConnectionState) => void>()
  private readonly reconnectedListeners = new Set<() => void>()
  private readonly closedListeners = new Set<() => void>()
  private readonly listeners: { [Name in HubEventName]: Set<Listener<Name>> } = {
    MessageReceived: new Set(),
    UserJoined: new Set(),
    UserLeft: new Set(),
    UserTyping: new Set(),
    PresenceChanged: new Set(),
  }
  private readonly typingSent: ConversationId[] = []
  private sentCounter = 0

  constructor(private readonly options: FakeChatHubOptions) {}

  async start(): Promise<void> {
    this.setState('connecting')
    this.setState('connected')
  }

  /** Our own stop: no `onClosed`, same as the SignalR client. */
  async stop(): Promise<void> {
    this.joined.clear()
    this.setState('disconnected')
  }

  currentState(): ConnectionState {
    return this.state
  }

  async joinRoom(conversationId: ConversationId): Promise<void> {
    this.requireConnected()
    requireKnownConversation(conversationId)
    this.joined.add(conversationId)
  }

  async leaveRoom(conversationId: ConversationId): Promise<void> {
    this.requireConnected()
    this.joined.delete(conversationId)
  }

  async sendMessage(conversationId: ConversationId, text: string): Promise<Message> {
    this.requireConnected()
    const author = this.options.currentUser()
    if (!author) throw new HubCallError('unauthenticated')
    const history = requireKnownConversation(conversationId)
    this.sentCounter += 1
    const message: Message = {
      id: `fake-${this.sentCounter}`,
      conversationId,
      author,
      text,
      sentAt: new Date().toISOString(),
    }
    history.push(message)
    this.emitMessage(message)
    return message
  }

  async typing(conversationId: ConversationId): Promise<void> {
    this.requireConnected()
    this.typingSent.push(conversationId)
  }

  on<Name extends HubEventName>(event: Name, handler: Listener<Name>): Unsubscribe {
    const set = this.listeners[event] as Set<Listener<Name>>
    set.add(handler)
    return () => set.delete(handler)
  }

  onConnectionState(handler: (state: ConnectionState) => void): Unsubscribe {
    this.stateListeners.add(handler)
    return () => this.stateListeners.delete(handler)
  }

  onReconnected(handler: () => void): Unsubscribe {
    this.reconnectedListeners.add(handler)
    return () => this.reconnectedListeners.delete(handler)
  }

  onClosed(handler: () => void): Unsubscribe {
    this.closedListeners.add(handler)
    return () => this.closedListeners.delete(handler)
  }

  /** Test helpers: what the server would push. */
  emitMessage(message: Message): void {
    this.emit('MessageReceived', message)
  }

  emitTyping(event: UserTypingEvent): void {
    this.emit('UserTyping', event)
  }

  emitPresence(change: PresenceChange): void {
    this.emit('PresenceChanged', change)
  }

  emitMemberJoined(event: ConversationMemberEvent): void {
    this.emit('UserJoined', event)
  }

  /** Drops the connection and brings it back, as an automatic reconnect does. */
  simulateReconnect(): void {
    this.setState('reconnecting')
    this.setState('connected')
    this.reconnectedListeners.forEach((listener) => listener())
  }

  /** The server closes the connection for good (session revoked): no reconnect follows. */
  simulateServerClose(): void {
    this.joined.clear()
    this.setState('disconnected')
    this.closedListeners.forEach((listener) => listener())
  }

  /** Test inspection: rooms currently joined through this connection. */
  joinedConversations(): ConversationId[] {
    return [...this.joined]
  }

  /** Test inspection: every `Typing` call the client made, in order. */
  typingCalls(): readonly ConversationId[] {
    return this.typingSent
  }

  private emit<Name extends HubEventName>(event: Name, payload: HubEventMap[Name]): void {
    const set = this.listeners[event] as Set<Listener<Name>>
    set.forEach((listener) => listener(payload))
  }

  private requireConnected(): void {
    if (this.state !== 'connected') throw new HubCallError('unknown')
  }

  private setState(next: ConnectionState): void {
    this.state = next
    this.stateListeners.forEach((listener) => listener(next))
  }
}

/** Unknown conversation (and, on the server, any non-member) answers `not_found`. */
function requireKnownConversation(conversationId: ConversationId): Message[] {
  const history = sharedMockHistories().get(conversationId)
  if (!history) throw new HubCallError('not_found')
  return history
}
