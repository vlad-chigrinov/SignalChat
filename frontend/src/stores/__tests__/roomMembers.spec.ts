import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { ApiError, api, type Room, type RoomMember, type UserSummary } from '@/api'
import type { RoomMemberJoinedEvent } from '@/realtime/hubContract'

import { useRoomMembersStore } from '../roomMembers'
import { useRoomsStore } from '../rooms'

const ROOM_ID = 'room-dev'
const IGOR: UserSummary = { id: 'u-igor', displayName: 'Игорь', avatarUrl: null }
const ME: UserSummary = { id: 'u-me', displayName: 'Вы', avatarUrl: null }
const ANNA: UserSummary = { id: 'u-anna', displayName: 'Анна', avatarUrl: null }

const LISTED: RoomMember[] = [
  { user: IGOR, role: 'owner' },
  { user: ME, role: 'member' },
]

function listedRoom(memberCount: number): Room {
  return {
    kind: 'room',
    id: ROOM_ID,
    name: 'Разработка',
    description: '',
    memberCount,
    lastMessage: null,
  }
}

function joined(user: UserSummary): RoomMemberJoinedEvent {
  return { roomId: ROOM_ID, user, joinedAt: '2026-11-02T10:00:00.000Z' }
}

describe('room members store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useRoomsStore().rooms = [listedRoom(2)]
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('loads the members of a room and keeps them apart per room', async () => {
    vi.spyOn(api, 'listRoomMembers').mockResolvedValue(LISTED)
    const store = useRoomMembersStore()

    await store.load(ROOM_ID)

    expect(store.entryOf(ROOM_ID).status).toBe('ready')
    expect(store.entryOf(ROOM_ID).items.map((member) => member.user.id)).toEqual([IGOR.id, ME.id])
    expect(store.entryOf('room-other').items).toEqual([])
  })

  it('keeps the room list count equal to the loaded members', async () => {
    vi.spyOn(api, 'listRoomMembers').mockResolvedValue([...LISTED, { user: ANNA, role: 'member' }])
    const rooms = useRoomsStore()

    await useRoomMembersStore().load(ROOM_ID)

    expect(rooms.rooms[0]?.memberCount).toBe(3)
  })

  it('shows the error text when loading fails, and loads again on retry', async () => {
    const listRoomMembers = vi
      .spyOn(api, 'listRoomMembers')
      .mockRejectedValueOnce(new ApiError(404, 'not found', 'not_found'))
      .mockResolvedValue(LISTED)
    const store = useRoomMembersStore()

    await store.load(ROOM_ID)
    expect(store.entryOf(ROOM_ID).status).toBe('error')
    expect(store.entryOf(ROOM_ID).error).toBe('Чат не найден.')

    await store.load(ROOM_ID)
    expect(store.entryOf(ROOM_ID).status).toBe('ready')
    expect(listRoomMembers).toHaveBeenCalledTimes(2)
  })

  it('adds a member who joins live, once, and updates the count', async () => {
    vi.spyOn(api, 'listRoomMembers').mockResolvedValue(LISTED)
    const store = useRoomMembersStore()
    const rooms = useRoomsStore()
    await store.load(ROOM_ID)

    store.applyJoined(joined(ANNA))
    store.applyJoined(joined(ANNA))

    expect(store.entryOf(ROOM_ID).items.map((member) => member.user.id)).toEqual([
      IGOR.id,
      ME.id,
      ANNA.id,
    ])
    expect(store.entryOf(ROOM_ID).items[2]?.role).toBe('member')
    expect(rooms.rooms[0]?.memberCount).toBe(3)
  })

  it('counts a live join in the room list when the members were never loaded', () => {
    useRoomMembersStore().applyJoined(joined(ANNA))

    expect(useRoomsStore().rooms[0]?.memberCount).toBe(3)
  })

  it('invites through the API and lists the invited user at once', async () => {
    vi.spyOn(api, 'listRoomMembers').mockResolvedValue(LISTED)
    const inviteToRoom = vi.spyOn(api, 'inviteToRoom').mockResolvedValue()
    const store = useRoomMembersStore()
    await store.load(ROOM_ID)

    await store.invite(ROOM_ID, ANNA)

    expect(inviteToRoom).toHaveBeenCalledWith(ROOM_ID, ANNA.id)
    expect(store.entryOf(ROOM_ID).items.map((member) => member.user.id)).toContain(ANNA.id)
    expect(useRoomsStore().rooms[0]?.memberCount).toBe(3)
  })

  it('lists nobody when the invitation is refused', async () => {
    vi.spyOn(api, 'listRoomMembers').mockResolvedValue(LISTED)
    vi.spyOn(api, 'inviteToRoom').mockRejectedValue(new ApiError(409, 'conflict', 'already_member'))
    const store = useRoomMembersStore()
    await store.load(ROOM_ID)

    await expect(store.invite(ROOM_ID, ANNA)).rejects.toBeInstanceOf(ApiError)

    expect(store.entryOf(ROOM_ID).items).toHaveLength(2)
  })

  it('shows a changed name and avatar of a member', async () => {
    vi.spyOn(api, 'listRoomMembers').mockResolvedValue(LISTED)
    const store = useRoomMembersStore()
    await store.load(ROOM_ID)

    store.applyProfileChange({ ...IGOR, displayName: 'Игорь Новый', avatarUrl: '/api/x?v=1' })

    expect(store.entryOf(ROOM_ID).items[0]?.user).toEqual({
      id: IGOR.id,
      displayName: 'Игорь Новый',
      avatarUrl: '/api/x?v=1',
    })
  })

  it('forgets everything on reset', async () => {
    vi.spyOn(api, 'listRoomMembers').mockResolvedValue(LISTED)
    const store = useRoomMembersStore()
    await store.load(ROOM_ID)

    store.reset()

    expect(store.entryOf(ROOM_ID).status).toBe('idle')
  })
})
