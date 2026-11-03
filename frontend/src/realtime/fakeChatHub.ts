import type { ConversationId, Message, PresenceChange, UserId, UserSummary } from '@/api'
import { historyOf, isActiveMember, sharedMockStore } from '@/api/mock/mockHistories'

import { HubCallError, type ChatHub, type Unsubscribe } from './chatHub'
import {
  type ConnectionState,
  type ConversationMemberEvent,
  type DirectRequestAcceptedEvent,
  type DirectRequestReceivedEvent,
  type HubEventMap,
  type HubEventName,
  type MyReactionChangedEvent,
  type ReactionsChangedEvent,
  type UserTypingEvent,
} from './hubContract'

type Listener<Name extends HubEventName> = (payload: HubEventMap[Name]) => void

export interface FakeChatHubOptions {
  /** Author of messages sent through the fake. Null means signed out (calls are rejected). */
  currentUser: () => UserSummary | null
}

/**
 * In-memory hub for unit tests and mock mode. It follows the same contract as the server:
 * a call needs a connection, and the caller must be a member of the conversation (else
 * `not_found`). A sent message is stored in the shared mock history and echoed to every
 * listener (sender included).
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
    DirectRequestReceived: new Set(),
    DirectRequestAccepted: new Set(),
    UserProfileChanged: new Set(),
    ReactionsChanged: new Set(),
    MyReactionChanged: new Set(),
  }
  private readonly typingSent: ConversationId[] = []
  private readonly queuedStartFailures: HubCallError[] = []
  private startCount = 0
  private sentCounter = 0

  constructor(private readonly options: FakeChatHubOptions) {}

  async start(): Promise<void> {
    this.startCount += 1
    this.setState('connecting')
    const failure = this.queuedStartFailures.shift()
    if (failure) {
      this.setState('disconnected')
      throw failure
    }
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
    const caller = this.requireCaller()
    requireConversationMember(caller.id, conversationId)
    this.joined.add(conversationId)
  }

  async leaveRoom(conversationId: ConversationId): Promise<void> {
    this.requireConnected()
    this.joined.delete(conversationId)
  }

  async sendMessage(conversationId: ConversationId, text: string): Promise<Message> {
    this.requireConnected()
    const author = this.requireCaller()
    requireWritable(author.id, conversationId)
    this.sentCounter += 1
    const message: Message = {
      id: `fake-${this.sentCounter}`,
      conversationId,
      author,
      text,
      sentAt: new Date().toISOString(),
      reactions: [],
    }
    historyOf(sharedMockStore(), conversationId).push(message)
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

  emitDirectRequestReceived(event: DirectRequestReceivedEvent): void {
    this.emit('DirectRequestReceived', event)
  }

  emitDirectRequestAccepted(event: DirectRequestAcceptedEvent): void {
    this.emit('DirectRequestAccepted', event)
  }

  emitProfileChanged(user: UserSummary): void {
    this.emit('UserProfileChanged', user)
  }

  emitReactionsChanged(event: ReactionsChangedEvent): void {
    this.emit('ReactionsChanged', event)
  }

  emitMyReactionChanged(event: MyReactionChangedEvent): void {
    this.emit('MyReactionChanged', event)
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

  /** Test helper: the next `start()` calls fail with these errors, one per call, in order. */
  failNextStarts(errors: HubCallError[]): void {
    this.queuedStartFailures.push(...errors)
  }

  /** Test inspection: how many times the client tried to start the connection. */
  startAttempts(): number {
    return this.startCount
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

  private requireCaller(): UserSummary {
    const caller = this.options.currentUser()
    if (!caller) throw new HubCallError('unauthenticated')
    return caller
  }

  private setState(next: ConnectionState): void {
    this.state = next
    this.stateListeners.forEach((listener) => listener(next))
  }
}

/** Unknown conversation and non-member answer the same `not_found` (C3). */
function requireConversationMember(userId: UserId, conversationId: ConversationId): void {
  if (!isActiveMember(sharedMockStore(), conversationId, userId)) {
    throw new HubCallError('not_found')
  }
}

/**
 * Writing is stricter than joining only for the writer of a request: until the recipient
 * accepts, the writer gets `dm_not_accepted`; anyone else gets `not_found`.
 */
function requireWritable(userId: UserId, conversationId: ConversationId): void {
  const store = sharedMockStore()
  if (isActiveMember(store, conversationId, userId)) return
  const direct = store.directs.get(conversationId)
  const isWriterOfRequest = direct !== undefined && direct.initiatorId === userId
  if (isWriterOfRequest && direct.memberIds.includes(userId)) {
    throw new HubCallError('dm_not_accepted')
  }
  throw new HubCallError('not_found')
}
