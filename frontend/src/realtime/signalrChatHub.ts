import * as signalR from '@microsoft/signalr'

import type { ConversationId, Message } from '@/api'

import { HubCallError, type ChatHub, type Unsubscribe } from './chatHub'
import {
  HUB_EVENTS,
  HUB_METHODS,
  HUB_PATH,
  RECONNECT_DELAYS_MS,
  type ConnectionState,
  type HubEventMap,
  type HubEventName,
} from './hubContract'
import { parseHubErrorCode } from './hubErrors'

/** Real hub client. The session cookie authenticates the connection: no token is handled here. */
export class SignalrChatHub implements ChatHub {
  private readonly connection: signalR.HubConnection
  private state: ConnectionState = 'disconnected'
  private readonly stateListeners = new Set<(state: ConnectionState) => void>()
  private readonly reconnectedListeners = new Set<() => void>()
  private readonly closedListeners = new Set<() => void>()
  /** Set by our own `stop()`, so that its close is not reported as a server-side close. */
  private stopRequested = false

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_PATH)
      .withAutomaticReconnect([...RECONNECT_DELAYS_MS])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    this.connection.onreconnecting(() => this.setState('reconnecting'))
    this.connection.onreconnected(() => {
      this.setState('connected')
      this.reconnectedListeners.forEach((listener) => listener())
    })
    this.connection.onclose(() => {
      this.setState('disconnected')
      if (!this.stopRequested) this.closedListeners.forEach((listener) => listener())
    })
  }

  async start(): Promise<void> {
    if (this.state === 'connected') return
    this.stopRequested = false
    this.setState('connecting')
    try {
      await this.connection.start()
      this.setState('connected')
    } catch (error) {
      this.setState('disconnected')
      throw toHubCallError(error)
    }
  }

  async stop(): Promise<void> {
    this.stopRequested = true
    await this.connection.stop()
    this.setState('disconnected')
  }

  currentState(): ConnectionState {
    return this.state
  }

  joinRoom(conversationId: ConversationId): Promise<void> {
    return this.invoke<void>(HUB_METHODS.JoinRoom, conversationId)
  }

  leaveRoom(conversationId: ConversationId): Promise<void> {
    return this.invoke<void>(HUB_METHODS.LeaveRoom, conversationId)
  }

  sendMessage(conversationId: ConversationId, text: string): Promise<Message> {
    return this.invoke<Message>(HUB_METHODS.SendMessage, conversationId, text)
  }

  typing(conversationId: ConversationId): Promise<void> {
    return this.invoke<void>(HUB_METHODS.Typing, conversationId)
  }

  on<Name extends HubEventName>(
    event: Name,
    handler: (payload: HubEventMap[Name]) => void,
  ): Unsubscribe {
    const wireHandler = (payload: HubEventMap[Name]) => handler(payload)
    this.connection.on(HUB_EVENTS[event], wireHandler)
    return () => this.connection.off(HUB_EVENTS[event], wireHandler)
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

  private async invoke<Result>(method: string, ...args: unknown[]): Promise<Result> {
    try {
      return await this.connection.invoke<Result>(method, ...args)
    } catch (error) {
      throw toHubCallError(error)
    }
  }

  private setState(next: ConnectionState): void {
    this.state = next
    this.stateListeners.forEach((listener) => listener(next))
  }
}

/** Keeps only the stable code: the raw SignalR message is never shown or logged by the UI. */
function toHubCallError(error: unknown): HubCallError {
  if (error instanceof HubCallError) return error
  const message = error instanceof Error ? error.message : ''
  return new HubCallError(parseHubErrorCode(message))
}
