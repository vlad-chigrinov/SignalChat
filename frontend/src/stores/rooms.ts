import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { api, type Conversation, type DirectConversation, type Message, type Room } from '@/api'
import { errorMessage } from '@/utils/errorMessage'

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error'

function byLatestActivity(left: Conversation, right: Conversation): number {
  const leftAt = left.lastMessage?.sentAt ?? ''
  const rightAt = right.lastMessage?.sentAt ?? ''
  return rightAt.localeCompare(leftAt)
}

/** Rooms and direct conversations the current user belongs to. */
export const useRoomsStore = defineStore('rooms', () => {
  const rooms = ref<Room[]>([])
  const directConversations = ref<DirectConversation[]>([])
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
    }
  }

  function reset(): void {
    rooms.value = []
    directConversations.value = []
    status.value = 'idle'
    error.value = null
  }

  return {
    rooms,
    directConversations,
    sortedRooms,
    sortedDirects,
    status,
    error,
    load,
    findConversation,
    applyLastMessage,
    reset,
  }
})
