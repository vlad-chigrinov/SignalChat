import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import {
  api,
  type ConversationId,
  type DirectConversation,
  type IncomingDirectRequest,
  type UserSummary,
} from '@/api'
import type { DirectRequestReceivedEvent } from '@/realtime/hubContract'

import { usePresenceStore } from './presence'
import { useRoomsStore } from './rooms'

/** The chat an accepted request becomes for the user who answered it. The request is its first message. */
function acceptedChatOf(request: IncomingDirectRequest): DirectConversation {
  return {
    kind: 'direct',
    id: request.id,
    peer: request.from,
    status: 'accepted',
    lastMessage: {
      authorId: request.from.id,
      text: request.text,
      sentAt: request.createdAt,
      photoCount: 0,
    },
  }
}

/** Requests waiting for the caller's answer. Filled by the list, then by the live event. */
export const useDirectRequestsStore = defineStore('directRequests', () => {
  const incoming = ref<IncomingDirectRequest[]>([])
  const count = computed(() => incoming.value.length)

  async function load(): Promise<void> {
    incoming.value = await api.listIncomingRequests()
  }

  /** Live: a request arrived while the user is connected. A reopened request replaces the old one. */
  function applyReceived(event: DirectRequestReceivedEvent): void {
    const request: IncomingDirectRequest = {
      id: event.conversationId,
      from: event.from,
      text: event.text,
      createdAt: event.createdAt,
    }
    incoming.value = [request, ...incoming.value.filter((item) => item.id !== request.id)]
  }

  function remove(conversationId: ConversationId): void {
    incoming.value = incoming.value.filter((item) => item.id !== conversationId)
  }

  /** Accepting opens the chat at once, so the user can write without a reload. */
  async function accept(conversationId: ConversationId): Promise<void> {
    const request = incoming.value.find((item) => item.id === conversationId)
    await api.acceptRequest(conversationId)
    remove(conversationId)
    const rooms = useRoomsStore()
    if (request) rooms.upsertDirect(acceptedChatOf(request))
    else await rooms.load({ force: true })
    // The sender is a peer only from now on, so the earlier snapshot of who is online does not list them.
    await usePresenceStore().loadSnapshot()
  }

  /** Silent for the writer: the request just leaves this list. */
  async function decline(conversationId: ConversationId): Promise<void> {
    await api.declineRequest(conversationId)
    remove(conversationId)
  }

  /** A sender changed their name or avatar. */
  function applyProfileChange(user: UserSummary): void {
    for (const request of incoming.value) {
      if (request.from.id === user.id) request.from = { ...user }
    }
  }

  function reset(): void {
    incoming.value = []
  }

  return { incoming, count, load, applyReceived, accept, decline, applyProfileChange, reset }
})
