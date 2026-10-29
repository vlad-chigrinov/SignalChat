import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { api, type DirectConversation, type Room } from '@/api'
import { DISCOVER_MAX_LIMIT } from '@/api/limits'

import { useRoomsStore } from '../rooms'

function room(id: string, name: string): Room {
  return { kind: 'room', id, name, description: '', memberCount: 1, lastMessage: null }
}

const DIRECT: DirectConversation = {
  kind: 'direct',
  id: 'dm-olga',
  peer: { id: 'u-olga', displayName: 'Ольга Ким', avatarUrl: null },
  lastMessage: null,
}

describe('rooms store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('discovers rooms with the maximum page size and keeps them apart from my rooms', async () => {
    const discoverRooms = vi
      .spyOn(api, 'discoverRooms')
      .mockResolvedValue([room('room-chess', 'Шахматы')])
    const store = useRoomsStore()

    await store.discover('шах')

    expect(discoverRooms).toHaveBeenCalledWith('шах', DISCOVER_MAX_LIMIT)
    expect(store.discovered.map((item) => item.id)).toEqual(['room-chess'])
    expect(store.rooms).toEqual([])
  })

  it('adds a created room to my rooms', async () => {
    const created = room('room-new', 'Новая')
    vi.spyOn(api, 'createRoom').mockResolvedValue(created)
    const store = useRoomsStore()

    await store.createRoom({ name: 'Новая', description: '' })

    expect(store.rooms).toEqual([created])
  })

  it('joins a discovered room, drops it from discovery and reloads my rooms', async () => {
    const joined = room('room-chess', 'Шахматы')
    vi.spyOn(api, 'discoverRooms').mockResolvedValue([joined])
    vi.spyOn(api, 'joinRoom').mockResolvedValue(undefined)
    vi.spyOn(api, 'listRooms').mockResolvedValue([joined])
    vi.spyOn(api, 'listDirectConversations').mockResolvedValue([])
    const store = useRoomsStore()
    await store.discover('')

    await store.joinRoom('room-chess')

    expect(store.discovered).toEqual([])
    expect(store.rooms.map((item) => item.id)).toEqual(['room-chess'])
  })

  it('leaves a room on the server and keeps it listed until forgotten', async () => {
    const general = room('room-general', 'Общий')
    const leaveRoom = vi.spyOn(api, 'leaveRoom').mockResolvedValue(undefined)
    const store = useRoomsStore()
    store.rooms = [general]

    await store.leaveRoom('room-general')
    expect(leaveRoom).toHaveBeenCalledWith('room-general')
    expect(store.rooms.map((item) => item.id)).toEqual(['room-general'])

    store.forgetRoom('room-general')
    expect(store.rooms).toEqual([])
  })

  it('opens a direct conversation once and reuses it on repeated calls', async () => {
    vi.spyOn(api, 'openDirectConversation').mockResolvedValue(DIRECT)
    const store = useRoomsStore()

    await store.openDirect('u-olga')
    await store.openDirect('u-olga')

    expect(store.directConversations.map((item) => item.id)).toEqual(['dm-olga'])
  })
})
