import { ref } from 'vue'
import { defineStore } from 'pinia'

import type { ConversationId, UserSummary } from '@/api'
import type { RoomMemberJoinedEvent } from '@/realtime/hubContract'
import type { RoomNotice } from '@/utils/timeline'

/** Only the newest notices of a room are kept: a long-open page must not grow without bound. */
export const MAX_NOTICES_PER_ROOM = 50

const NO_NOTICES: readonly RoomNotice[] = Object.freeze([])

/**
 * "Anna joined" lines of the rooms this page has had open. They live in memory only: they are not history, so
 * they are not stored, not paged, and gone after a reload (a member who joined earlier is in the member list).
 */
export const useRoomNoticesStore = defineStore('roomNotices', () => {
  const byRoom = ref<Record<ConversationId, RoomNotice[]>>({})

  function noticesOf(roomId: ConversationId): readonly RoomNotice[] {
    return byRoom.value[roomId] ?? NO_NOTICES
  }

  function applyMemberJoined(event: RoomMemberJoinedEvent): void {
    const id = `${event.roomId}:${event.user.id}:${event.joinedAt}`
    const current = byRoom.value[event.roomId] ?? []
    if (current.some((notice) => notice.id === id)) return
    const notice: RoomNotice = {
      id,
      kind: 'member-joined',
      user: { ...event.user },
      at: event.joinedAt,
    }
    byRoom.value[event.roomId] = [...current, notice].slice(-MAX_NOTICES_PER_ROOM)
  }

  /** A member changed their name or avatar. */
  function applyProfileChange(user: UserSummary): void {
    for (const notices of Object.values(byRoom.value)) {
      for (const notice of notices) {
        if (notice.user.id === user.id) notice.user = { ...user }
      }
    }
  }

  function reset(): void {
    byRoom.value = {}
  }

  return { byRoom, noticesOf, applyMemberJoined, applyProfileChange, reset }
})
