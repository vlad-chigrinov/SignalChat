import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { api, type Room, type UserSummary } from '@/api'
import { mockCurrentUser } from '@/api/mock/mockData'
import { resetSharedMockStore, sharedMockStore } from '@/api/mock/mockHistories'
import { inviteToRoom } from '@/api/mock/mockRoomMembers'
import { useRoomMembersStore } from '@/stores/roomMembers'
import { useRoomNoticesStore } from '@/stores/roomNotices'
import { usePresenceStore } from '@/stores/presence'
import { useRoomsStore } from '@/stores/rooms'

import { FakeChatHub } from '../fakeChatHub'
import { createRealtimeService, type RealtimeService } from '../realtimeService'

const ME: UserSummary = {
  id: mockCurrentUser.id,
  displayName: mockCurrentUser.displayName,
  avatarUrl: mockCurrentUser.avatarUrl,
}
const ANNA: UserSummary = { id: 'u-anna', displayName: 'Анна Смирнова', avatarUrl: null }
const ROOM = 'room-dev'
const JOINED_AT = '2026-11-02T10:00:00.000Z'

const DEV_ROOM: Room = {
  kind: 'room',
  id: ROOM,
  name: 'Разработка',
  description: 'Код и релизы',
  memberCount: 2,
  lastMessage: null,
}

describe('realtime service: room members', () => {
  let hub: FakeChatHub
  let service: RealtimeService

  beforeEach(() => {
    vi.restoreAllMocks()
    resetSharedMockStore()
    setActivePinia(createPinia())
    hub = new FakeChatHub({ currentUser: () => ME })
    service = createRealtimeService(hub)
  })

  afterEach(() => {
    service.dispose()
  })

  it('shows a "joined" notice, lists the member and counts them when someone joins', () => {
    const rooms = useRoomsStore()
    rooms.rooms = [{ ...DEV_ROOM }]

    hub.emitRoomMemberJoined({ roomId: ROOM, user: ANNA, joinedAt: JOINED_AT })

    expect(useRoomNoticesStore().noticesOf(ROOM)).toMatchObject([
      { kind: 'member-joined', user: { id: ANNA.id }, at: JOINED_AT },
    ])
    expect(rooms.rooms[0]?.memberCount).toBe(3)
  })

  it('adds a loaded member list entry once, however often the event comes', async () => {
    vi.spyOn(api, 'listRoomMembers').mockResolvedValue([{ user: ME, role: 'member' }])
    useRoomsStore().rooms = [{ ...DEV_ROOM }]
    const members = useRoomMembersStore()
    await members.load(ROOM)

    hub.emitRoomMemberJoined({ roomId: ROOM, user: ANNA, joinedAt: JOINED_AT })
    hub.emitRoomMemberJoined({ roomId: ROOM, user: ANNA, joinedAt: JOINED_AT })

    expect(members.entryOf(ROOM).items.map((member) => member.user.id)).toEqual([ME.id, ANNA.id])
  })

  it('lists the room at once for an invitee, and loads who is online in it', async () => {
    const getOnlineUserIds = vi.spyOn(api, 'getOnlineUserIds').mockResolvedValue([ANNA.id])

    hub.emitAddedToRoom({ room: DEV_ROOM })
    await vi.waitFor(() => expect(getOnlineUserIds).toHaveBeenCalled())

    expect(useRoomsStore().rooms.map((room) => room.id)).toEqual([ROOM])
    expect(usePresenceStore().isOnline(ANNA.id)).toBe(true)
  })

  it('does not show a notice to an invitation made in another room', () => {
    hub.emitRoomMemberJoined({ roomId: 'room-general', user: ANNA, joinedAt: JOINED_AT })

    expect(useRoomNoticesStore().noticesOf(ROOM)).toEqual([])
  })
})

describe('fake hub: the server events of a membership change', () => {
  let hub: FakeChatHub
  let service: RealtimeService

  beforeEach(() => {
    vi.restoreAllMocks()
    resetSharedMockStore()
    setActivePinia(createPinia())
    hub = new FakeChatHub({ currentUser: () => ME })
    service = createRealtimeService(hub)
  })

  afterEach(() => {
    service.dispose()
  })

  it('pushes the notice to a connection that has the room open, the inviter included', async () => {
    await hub.start()
    await hub.joinRoom(ROOM)

    inviteToRoom(sharedMockStore(), ME.id, ROOM, ANNA.id)

    expect(
      useRoomNoticesStore()
        .noticesOf(ROOM)
        .map((notice) => notice.user.id),
    ).toEqual([ANNA.id])
  })

  it('pushes nothing to a connection that did not open the room', async () => {
    await hub.start()

    inviteToRoom(sharedMockStore(), ME.id, ROOM, ANNA.id)

    expect(useRoomNoticesStore().noticesOf(ROOM)).toEqual([])
  })

  it('pushes nothing once the connection is stopped', async () => {
    await hub.start()
    await hub.joinRoom(ROOM)
    await hub.stop()

    inviteToRoom(sharedMockStore(), ME.id, ROOM, ANNA.id)

    expect(useRoomNoticesStore().noticesOf(ROOM)).toEqual([])
  })
})
