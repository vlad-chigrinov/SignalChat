import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import {
  api,
  type Conversation,
  type CreateRoomInput,
  type DirectConversation,
  type ConversationId,
  type Message,
  type Room,
  type UserId,
  type UserSummary,
} from '@/api'
import { DISCOVER_MAX_LIMIT } from '@/api/limits'
import { errorMessage } from '@/utils/errorMessage'

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error'

function byLatestActivity(left: Conversation, right: Conversation): number {
  const leftAt = left.lastMessage?.sentAt ?? ''
  const rightAt = right.lastMessage?.sentAt ?? ''
  return rightAt.localeCompare(leftAt)
}

/**
 * Rooms and direct conversations the current user belongs to, plus rooms found by discovery
 * (which the user is not in yet). Actions throw the API error; the caller shows `errorMessage`.
 */
export const useRoomsStore = defineStore('rooms', () => {
  const rooms = ref<Room[]>([])
  const directConversations = ref<DirectConversation[]>([])
  const discovered = ref<Room[]>([])
  const status = ref<LoadStatus>('idle')
  const error = ref<string | null>(null)

  const sortedRooms = computed(() => [...rooms.value].sort(byLatestActivity))
  const sortedDirects = computed(() => [...directConversations.value].sort(byLatestActivity))

  async function load({ force = false } = {}): Promise<void> {
    if (status.value === 'loading' || (status.value === 'ready' && !force)) return
    status.value = 'loading'
    try {
      const [loadedRooms, loadedDirects] = await Promise.all([
        api.listRooms(),
        api.listDirectConversations(),
      ])
      rooms.value = loadedRooms
      directConversations.value = loadedDirects
      error.value = null
      status.value = 'ready'
    } catch (caught) {
      error.value = errorMessage(caught)
      status.value = 'error'
    }
  }

  function findConversation(id: string): Conversation | undefined {
    return (
      rooms.value.find((room) => room.id === id) ??
      directConversations.value.find((dm) => dm.id === id)
    )
  }

  /** Keeps the list preview in sync with a new message (sent or received in real time). */
  function applyLastMessage(message: Message): void {
    const conversation = findConversation(message.conversationId)
    if (!conversation) return
    const current = conversation.lastMessage
    if (current && current.sentAt > message.sentAt) return
    conversation.lastMessage = {
      authorId: message.author.id,
      text: message.text,
      sentAt: message.sentAt,
      photoCount: message.attachments.length,
    }
  }

  /** Rooms matching `query` that the caller is not in. Replaces the previous result. */
  async function discover(query: string): Promise<void> {
    discovered.value = await api.discoverRooms(query, DISCOVER_MAX_LIMIT)
  }

  async function createRoom(input: CreateRoomInput): Promise<Room> {
    const room = await api.createRoom(input)
    rooms.value = [...rooms.value, room]
    return room
  }

  /** Joins a discovered room. The list is reloaded so the member count matches the server. */
  async function joinRoom(roomId: ConversationId): Promise<void> {
    await api.joinRoom(roomId)
    discovered.value = discovered.value.filter((room) => room.id !== roomId)
    await load({ force: true })
  }

  /**
   * Leaves on the server. The room stays in the list until `forgetRoom`: a room view must
   * navigate away first, or it would show "not found" for the room it is still on.
   */
  async function leaveRoom(roomId: ConversationId): Promise<void> {
    await api.leaveRoom(roomId)
  }

  function forgetRoom(roomId: ConversationId): void {
    rooms.value = rooms.value.filter((room) => room.id !== roomId)
  }

  /** Adds a direct conversation, or replaces the listed one with the same id. */
  function upsertDirect(direct: DirectConversation): void {
    const others = directConversations.value.filter((item) => item.id !== direct.id)
    directConversations.value = [...others, direct]
  }

  /**
   * Writes the first message to a user: a request, unless the chat exists already. The answer is
   * listed with its status (`pending` while the peer has not answered).
   */
  async function sendDirectRequest(userId: UserId, text: string): Promise<DirectConversation> {
    const direct = await api.sendDirectRequest(userId, text)
    upsertDirect(direct)
    return direct
  }

  /**
   * The peer accepted the request this user wrote: the chat is open for writing now. A chat that
   * is not listed yet is loaded with the list.
   */
  async function markAccepted(conversationId: ConversationId): Promise<void> {
    const direct = directConversations.value.find((item) => item.id === conversationId)
    if (direct) {
      direct.status = 'accepted'
      return
    }
    await load({ force: true })
  }

  /**
   * Reloads the direct conversations without the loading state: after a reconnect, so that a
   * request accepted meanwhile shows up, and the room list does not flicker.
   */
  async function refreshDirects(): Promise<void> {
    directConversations.value = await api.listDirectConversations()
  }

  /** A peer changed their name or avatar. The summary is copied, so no list shares an object with another. */
  function applyProfileChange(user: UserSummary): void {
    for (const direct of directConversations.value) {
      if (direct.peer.id === user.id) direct.peer = { ...user }
    }
  }

  function reset(): void {
    rooms.value = []
    directConversations.value = []
    discovered.value = []
    status.value = 'idle'
    error.value = null
  }

  return {
    rooms,
    directConversations,
    discovered,
    sortedRooms,
    sortedDirects,
    status,
    error,
    load,
    findConversation,
    applyLastMessage,
    discover,
    createRoom,
    joinRoom,
    leaveRoom,
    forgetRoom,
    upsertDirect,
    sendDirectRequest,
    markAccepted,
    refreshDirects,
    applyProfileChange,
    reset,
  }
})
