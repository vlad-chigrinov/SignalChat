import { api, type ConversationId, type Message } from '@/api'
import { useConnectionStore } from '@/stores/connection'
import { useMessagesStore } from '@/stores/messages'
import { usePresenceStore } from '@/stores/presence'
import { useTypingStore } from '@/stores/typing'
import { normalizeMessageText } from '@/utils/messageText'

import { HubCallError, NotConnectedError, type ChatHub, type Unsubscribe } from './chatHub'
import { RECONNECT_DELAYS_MS, TYPING_SEND_INTERVAL_MS } from './hubContract'

/** Delay before retry number `attempt` (0-based). The last delay repeats once the list runs out. */
function retryDelayMs(attempt: number): number {
  const lastIndex = RECONNECT_DELAYS_MS.length - 1
  return RECONNECT_DELAYS_MS[Math.min(attempt, lastIndex)] ?? 0
}

function sleep(milliseconds: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, milliseconds))
}

export interface RealtimeService {
  /** Starts the hub and joins the conversations that are already open. */
  connect(): Promise<void>
  /** Stops the hub and forgets open conversations and typing state. */
  disconnect(): Promise<void>
  /** Joins the conversation's group. Rejects with `HubCallError` (e.g. `not_found`). */
  openConversation(conversationId: ConversationId): Promise<void>
  /** Leaves the group. Best effort: the server also drops the membership on disconnect. */
  closeConversation(conversationId: ConversationId): Promise<void>
  /**
   * Sends through the hub only (C1). Rejects with `NotConnectedError` before any call when the
   * hub is not connected, and with `HubCallError` for a hub error.
   */
  sendMessage(conversationId: ConversationId, text: string): Promise<Message>
  /** Tells the others that the user is typing; throttled per conversation. */
  notifyTyping(conversationId: ConversationId): void
  /** Removes the hub subscriptions. */
  dispose(): void
}

/**
 * Connects the hub to the stores. Owns the set of open conversations, which
 * is what gets joined again after a reconnect (groups are per connection).
 */
export function createRealtimeService(hub: ChatHub): RealtimeService {
  const messages = useMessagesStore()
  const presence = usePresenceStore()
  const typing = useTypingStore()
  const connection = useConnectionStore()

  const openConversationIds = new Set<ConversationId>()
  const lastTypingSentAt = new Map<ConversationId, number>()
  /** Bumped by every connect and disconnect: a retry loop from an older generation stops. */
  let connectGeneration = 0

  function isConnected(): boolean {
    return hub.currentState() === 'connected'
  }

  async function joinOpenConversations(): Promise<void> {
    await Promise.allSettled([...openConversationIds].map((id) => hub.joinRoom(id)))
  }

  /** Groups are gone after a reconnect: join again, then catch up on what was missed. */
  async function restoreAfterReconnect(): Promise<void> {
    const ids = [...openConversationIds]
    await joinOpenConversations()
    await Promise.allSettled([
      ...ids.map((id) => messages.refreshLatest(id)),
      presence.loadSnapshot(),
    ])
  }

  /**
   * The server closed the connection with no reconnect. The network may simply be gone, so
   * the session is checked: only an answer "not signed in" (401) ends it.
   */
  async function handleServerClose(): Promise<void> {
    const user = await api.getCurrentUser().catch(() => undefined)
    if (user === null) connection.markSessionEnded()
  }

  const unsubscribers: Unsubscribe[] = [
    hub.on('MessageReceived', (message) => {
      messages.applyIncomingMessage(message)
      typing.clearUser(message.conversationId, message.author.id)
    }),
    hub.on('PresenceChanged', (change) => presence.applyPresenceChanged(change)),
    hub.on('UserTyping', (event) => typing.applyUserTyping(event)),
    hub.onConnectionState((state) => connection.setState(state)),
    hub.onReconnected(() => void restoreAfterReconnect()),
    hub.onClosed(() => void handleServerClose()),
  ]

  /**
   * Starts the hub, retrying with RECONNECT_DELAYS_MS until it works or `disconnect()` is called.
   * A 401 ends the session at once: retrying cannot help, so it is not retried.
   */
  async function connect(): Promise<void> {
    connectGeneration += 1
    const generation = connectGeneration
    for (let attempt = 0; ; attempt += 1) {
      try {
        await hub.start()
        break
      } catch (error) {
        if (error instanceof HubCallError && error.code === 'unauthenticated') {
          connection.markSessionEnded()
          throw error
        }
        if (generation !== connectGeneration) return
        await sleep(retryDelayMs(attempt))
        if (generation !== connectGeneration) return
      }
    }
    await joinOpenConversations()
  }

  async function disconnect(): Promise<void> {
    connectGeneration += 1
    openConversationIds.clear()
    lastTypingSentAt.clear()
    typing.reset()
    await hub.stop()
  }

  async function openConversation(conversationId: ConversationId): Promise<void> {
    openConversationIds.add(conversationId)
    if (!isConnected()) return
    try {
      await hub.joinRoom(conversationId)
    } catch (error) {
      openConversationIds.delete(conversationId)
      throw error
    }
  }

  async function closeConversation(conversationId: ConversationId): Promise<void> {
    openConversationIds.delete(conversationId)
    lastTypingSentAt.delete(conversationId)
    typing.clearConversation(conversationId)
    if (!isConnected()) return
    await hub.leaveRoom(conversationId).catch(() => undefined)
  }

  async function sendMessage(conversationId: ConversationId, rawText: string): Promise<Message> {
    const text = normalizeMessageText(rawText)
    if (!isConnected()) throw new NotConnectedError()
    const message = await hub.sendMessage(conversationId, text)
    // The hub echoes the message to the group too: mergeMessages keeps only one copy.
    messages.applyIncomingMessage(message)
    return message
  }

  function notifyTyping(conversationId: ConversationId): void {
    if (!isConnected()) return
    const now = Date.now()
    const lastSentAt = lastTypingSentAt.get(conversationId)
    if (lastSentAt !== undefined && now - lastSentAt < TYPING_SEND_INTERVAL_MS) return
    lastTypingSentAt.set(conversationId, now)
    // Typing is a hint, not a message: a failed call is not worth reporting.
    void hub.typing(conversationId).catch(() => undefined)
  }

  function dispose(): void {
    unsubscribers.forEach((unsubscribe) => unsubscribe())
  }

  return {
    connect,
    disconnect,
    openConversation,
    closeConversation,
    sendMessage,
    notifyTyping,
    dispose,
  }
}
