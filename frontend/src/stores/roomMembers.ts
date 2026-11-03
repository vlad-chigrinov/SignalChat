import { ref } from 'vue'
import { defineStore } from 'pinia'

import { api, type ConversationId, type RoomMember, type UserSummary } from '@/api'
import { ROOM_MEMBERS_MAX_COUNT } from '@/api/limits'
import type { RoomMemberJoinedEvent } from '@/realtime/hubContract'
import { errorMessage } from '@/utils/errorMessage'

import { useRoomsStore } from './rooms'

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error'

export interface RoomMembersEntry {
  items: RoomMember[]
  status: LoadStatus
  error: string | null
}

function emptyEntry(): RoomMembersEntry {
  return { items: [], status: 'idle', error: null }
}

const EMPTY_ENTRY: Readonly<RoomMembersEntry> = Object.freeze(emptyEntry())

/**
 * Who is in each room the user has looked at. The list is loaded when the dialog opens, then kept current by the live
 * "joined" event and by the user's own invitations. Presence is not kept here: it comes from the presence store.
 */
export const useRoomMembersStore = defineStore('roomMembers', () => {
  const byRoom = ref<Record<ConversationId, RoomMembersEntry>>({})

  function entryOf(roomId: ConversationId): Readonly<RoomMembersEntry> {
    return byRoom.value[roomId] ?? EMPTY_ENTRY
  }

  function ensureEntry(roomId: ConversationId): RoomMembersEntry {
    byRoom.value[roomId] ??= emptyEntry()
    return byRoom.value[roomId]
  }

  /** Always reads the server: a dialog that opens must not show a list from an hour ago. */
  async function load(roomId: ConversationId): Promise<void> {
    const entry = ensureEntry(roomId)
    entry.status = 'loading'
    try {
      entry.items = await api.listRoomMembers(roomId)
      entry.error = null
      entry.status = 'ready'
      syncCount(roomId, entry)
    } catch (caught) {
      entry.error = errorMessage(caught)
      entry.status = 'error'
    }
  }

  /**
   * The room list shows the number of members. A full list is the exact number; a cut one (the server caps it)
   * says nothing about the total, so the count stays.
   */
  function syncCount(roomId: ConversationId, entry: RoomMembersEntry): void {
    if (entry.items.length < ROOM_MEMBERS_MAX_COUNT) {
      useRoomsStore().setMemberCount(roomId, entry.items.length)
    }
  }

  /** True when the member was added; false when the list already had them. */
  function addMember(roomId: ConversationId, user: UserSummary): boolean {
    const entry = byRoom.value[roomId]
    if (!entry || entry.status !== 'ready') return false
    if (entry.items.some((member) => member.user.id === user.id)) return false
    entry.items = [...entry.items, { user: { ...user }, role: 'member' }]
    syncCount(roomId, entry)
    return true
  }

  /**
   * Live: someone joined. A loaded list gets the member once. A room whose members were never loaded only has its
   * count raised, so the list view is right without a reload.
   */
  function applyJoined(event: RoomMemberJoinedEvent): void {
    const entry = byRoom.value[event.roomId]
    if (entry?.status === 'ready') {
      addMember(event.roomId, event.user)
      return
    }
    useRoomsStore().bumpMemberCount(event.roomId)
  }

  /**
   * The user's own invitation. The live event that follows finds the member listed and changes nothing. The
   * dialog loads the list first, so the member is always added here and counted once.
   */
  async function invite(roomId: ConversationId, user: UserSummary): Promise<void> {
    await api.inviteToRoom(roomId, user.id)
    addMember(roomId, user)
  }

  /** A member changed their name or avatar. */
  function applyProfileChange(user: UserSummary): void {
    for (const entry of Object.values(byRoom.value)) {
      for (const member of entry.items) {
        if (member.user.id === user.id) member.user = { ...user }
      }
    }
  }

  function reset(): void {
    byRoom.value = {}
  }

  return { byRoom, entryOf, load, applyJoined, invite, applyProfileChange, reset }
})
