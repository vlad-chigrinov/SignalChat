import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { api, type Room } from '@/api'

import { usePresenceStore } from '../presence'
import { useRoomsStore } from '../rooms'

function room(id: string, name: string, memberCount = 1): Room {
  return { kind: 'room', id, name, description: '', memberCount, lastMessage: null }
}

describe('rooms store: members and invitations', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('lists a room the user was added to', () => {
    const store = useRoomsStore()

    store.applyAddedToRoom(room('room-dev', 'Разработка', 2))

    expect(store.rooms.map((item) => item.id)).toEqual(['room-dev'])
  })

  it('replaces the listed room instead of listing it twice', () => {
    const store = useRoomsStore()
    store.rooms = [room('room-dev', 'Разработка', 2)]

    store.applyAddedToRoom(room('room-dev', 'Разработка', 3))

    expect(store.rooms).toHaveLength(1)
    expect(store.rooms[0]?.memberCount).toBe(3)
  })

  it('counts members by one, and sets an exact count', () => {
    const store = useRoomsStore()
    store.rooms = [room('room-dev', 'Разработка', 2)]

    store.bumpMemberCount('room-dev')
    expect(store.rooms[0]?.memberCount).toBe(3)

    store.setMemberCount('room-dev', 7)
    expect(store.rooms[0]?.memberCount).toBe(7)
  })

  it('ignores a count for a room that is not listed', () => {
    const store = useRoomsStore()

    store.bumpMemberCount('room-unknown')
    store.setMemberCount('room-unknown', 4)

    expect(store.rooms).toEqual([])
  })

  it('loads who is online again after joining a room: its members are peers now', async () => {
    vi.spyOn(api, 'joinRoom').mockResolvedValue()
    vi.spyOn(api, 'listRooms').mockResolvedValue([room('room-chess', 'Шахматы', 3)])
    vi.spyOn(api, 'listDirectConversations').mockResolvedValue([])
    const getOnlineUserIds = vi.spyOn(api, 'getOnlineUserIds').mockResolvedValue(['u-anna'])
    const store = useRoomsStore()

    await store.joinRoom('room-chess')

    expect(getOnlineUserIds).toHaveBeenCalledTimes(1)
    expect(usePresenceStore().isOnline('u-anna')).toBe(true)
  })
})
