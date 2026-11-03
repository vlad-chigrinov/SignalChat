import { api, type ConversationId, type Message, type Room } from '@/api'
import { useAuthStore } from '@/stores/auth'
import { useConnectionStore } from '@/stores/connection'
import { useDirectRequestsStore } from '@/stores/directRequests'
import { useMessagesStore } from '@/stores/messages'
import { usePresenceStore } from '@/stores/presence'
import { applyProfileChange } from '@/stores/profileSync'
import { useRoomMembersStore } from '@/stores/roomMembers'
import { useRoomNoticesStore } from '@/stores/roomNotices'
import { useRoomsStore } from '@/stores/rooms'
import { useTypingStore } from '@/stores/typing'
import { normalizeMessageText } from '@/utils/messageText'

import { HubCallError, NotConnectedError, type ChatHub, type Unsubscribe } from './chatHub'
import { TYPING_SEND_INTERVAL_MS, TYPING_TRAILING_MARGIN_MS } from './hubContract'
import { retryDelayMs } from './retryDelay'

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
  const auth = useAuthStore()
  const messages = useMessagesStore()
  const presence = usePresenceStore()
  const typing = useTypingStore()
  const connection = useConnectionStore()
  const rooms = useRoomsStore()
  const directRequests = useDirectRequestsStore()
  const roomMembers = useRoomMembersStore()
  const roomNotices = useRoomNoticesStore()

  const openConversationIds = new Set<ConversationId>()
  const lastTypingSentAt = new Map<ConversationId, number>()
  /** One pending trailing hint per conversation, sent when the throttle window has passed. */
  const trailingTypingTimers = new Map<ConversationId, ReturnType<typeof setTimeout>>()
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
      rooms.refreshDirects(),
      directRequests.load(),
    ])
  }

  /**
   * The peer accepted our request: the chat is writable now, and its group is joined. The peer
   * shares presence with us only from now on, so who is online is loaded again.
   */
  async function handleRequestAccepted(conversationId: ConversationId): Promise<void> {
    await rooms.markAccepted(conversationId).catch(() => undefined)
    await openConversation(conversationId).catch(() => undefined)
    await presence.loadSnapshot()
  }

  /**
   * An invitation added the user to a room: it is listed at once. Its members are peers from now on, so who is
   * online is loaded again (the snapshot from before does not list them).
   */
  async function handleAddedToRoom(room: Room): Promise<void> {
    rooms.applyAddedToRoom(room)
    await presence.loadSnapshot()
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
    hub.on('DirectRequestReceived', (event) => directRequests.applyReceived(event)),
    hub.on('DirectRequestAccepted', (event) => void handleRequestAccepted(event.conversationId)),
    hub.on('UserProfileChanged', (user) => {
      auth.applyOwnProfile(user)
      applyProfileChange(user)
    }),
    hub.on('ReactionsChanged', (event) => messages.applyReactionsChanged(event)),
    hub.on('MyReactionChanged', (event) => messages.applyMyReactionChanged(event)),
    hub.on('RoomMemberJoined', (event) => {
      roomNotices.applyMemberJoined(event)
      roomMembers.applyJoined(event)
    }),
    hub.on('AddedToRoom', (event) => void handleAddedToRoom(event.room)),
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
    // A request sent while this connection was starting was not pushed to it: load the list once more.
    await directRequests.load().catch(() => undefined)
  }

  async function disconnect(): Promise<void> {
    connectGeneration += 1
    openConversationIds.clear()
    lastTypingSentAt.clear()
    trailingTypingTimers.forEach((timer) => clearTimeout(timer))
    trailingTypingTimers.clear()
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
    cancelTrailingTyping(conversationId)
    typing.clearConversation(conversationId)
    if (!isConnected()) return
    await hub.leaveRoom(conversationId).catch(() => undefined)
  }

  async function sendMessage(conversationId: ConversationId, rawText: string): Promise<Message> {
    const text = normalizeMessageText(rawText)
    if (!isConnected()) throw new NotConnectedError()
    // A sent message ends the typing, so a pending hint for this conversation is dropped.
    cancelTrailingTyping(conversationId)
    const message = await hub.sendMessage(conversationId, text)
    // The hub echoes the message to the group too: mergeMessages keeps only one copy.
    messages.applyIncomingMessage(message)
    return message
  }

  function sendTyping(conversationId: ConversationId): void {
    if (!isConnected()) return
    cancelTrailingTyping(conversationId)
    lastTypingSentAt.set(conversationId, Date.now())
    // Typing is a hint, not a message: a failed call is not worth reporting.
    void hub.typing(conversationId).catch(() => undefined)
  }

  function cancelTrailingTyping(conversationId: ConversationId): void {
    const timer = trailingTypingTimers.get(conversationId)
    if (timer === undefined) return
    clearTimeout(timer)
    trailingTypingTimers.delete(conversationId)
  }

  /** Inside the throttle window the keystroke is not lost: one hint is sent when the window ends. */
  function scheduleTrailingTyping(conversationId: ConversationId, delayMs: number): void {
    if (trailingTypingTimers.has(conversationId)) return
    const timer = setTimeout(() => {
      trailingTypingTimers.delete(conversationId)
      sendTyping(conversationId)
    }, delayMs)
    trailingTypingTimers.set(conversationId, timer)
  }

  function notifyTyping(conversationId: ConversationId): void {
    if (!isConnected()) return
    const lastSentAt = lastTypingSentAt.get(conversationId)
    const elapsedMs = lastSentAt === undefined ? TYPING_SEND_INTERVAL_MS : Date.now() - lastSentAt
    if (elapsedMs >= TYPING_SEND_INTERVAL_MS) {
      sendTyping(conversationId)
      return
    }
    scheduleTrailingTyping(
      conversationId,
      TYPING_SEND_INTERVAL_MS - elapsedMs + TYPING_TRAILING_MARGIN_MS,
    )
  }

  function dispose(): void {
    unsubscribers.forEach((unsubscribe) => unsubscribe())
    trailingTypingTimers.forEach((timer) => clearTimeout(timer))
    trailingTypingTimers.clear()
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
