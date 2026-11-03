import { describe, expect, it } from 'vitest'

import { ApiError } from '../http'
import { createSeedStore, mockCurrentUser } from '../mock/mockData'
import { joinRoom } from '../mock/mockRooms'
import { inviteToRoom, listRoomMembers } from '../mock/mockRoomMembers'
import { subscribeMockServerEvents, type MockServerEvent } from '../mock/mockHistories'

const ME = mockCurrentUser.id
const ANNA = 'u-anna'
const IGOR = 'u-igor'
const OLGA = 'u-olga'
const PETR = 'u-petr'
/** Igor owns it and the demo user is a member; Anna has a chat with the demo user but is not in it. */
const DEV_ROOM = 'room-dev'
/** The demo user is not a member. */
const CHESS_ROOM = 'room-chess'

function expectCode(action: () => unknown, code: string): void {
  let caught: unknown = null
  try {
    action()
  } catch (error) {
    caught = error
  }
  expect(caught).toBeInstanceOf(ApiError)
  expect((caught as ApiError).code).toBe(code)
}

function recordEvents(store: ReturnType<typeof createSeedStore>): MockServerEvent[] {
  const events: MockServerEvent[] = []
  subscribeMockServerEvents(store, (event) => events.push(event))
  return events
}

describe('mock room members: the list', () => {
  it('lists the owner first, with the current public profile and the role', () => {
    const store = createSeedStore()

    const members = listRoomMembers(store, ME, DEV_ROOM)

    expect(members.map((member) => [member.user.id, member.role])).toEqual([
      [IGOR, 'owner'],
      [ME, 'member'],
    ])
    expect(members[0]?.user).toEqual({ id: IGOR, displayName: 'Игорь Петров', avatarUrl: null })
  })

  it('shows the name the user has now', () => {
    const store = createSeedStore()
    store.currentUser.displayName = 'Новое имя'

    const mine = listRoomMembers(store, ME, DEV_ROOM).find((member) => member.user.id === ME)

    expect(mine?.user.displayName).toBe('Новое имя')
  })

  it('never carries an email', () => {
    const members = listRoomMembers(createSeedStore(), ME, DEV_ROOM)

    expect(JSON.stringify(members)).not.toContain('email')
    expect(JSON.stringify(members)).not.toContain('@')
  })

  it('answers not_found to a non-member and for a missing room, the same way', () => {
    const store = createSeedStore()

    expectCode(() => listRoomMembers(store, ME, CHESS_ROOM), 'not_found')
    expectCode(() => listRoomMembers(store, ME, 'room-missing'), 'not_found')
  })
})

describe('mock room members: invitations', () => {
  it('adds a chat partner at once, without a confirmation', () => {
    const store = createSeedStore()

    inviteToRoom(store, ME, DEV_ROOM, ANNA)

    expect(store.rooms.get(DEV_ROOM)?.memberIds.has(ANNA)).toBe(true)
    expect(listRoomMembers(store, ME, DEV_ROOM).map((member) => member.user.id)).toContain(ANNA)
  })

  it('refuses a user the inviter has no accepted chat with, like an unknown id', () => {
    const store = createSeedStore()

    expectCode(() => inviteToRoom(store, ME, DEV_ROOM, PETR), 'not_found')
    expectCode(() => inviteToRoom(store, ME, DEV_ROOM, 'u-nobody'), 'not_found')
    expectCode(() => inviteToRoom(store, ME, DEV_ROOM, ME), 'not_found')
  })

  it('refuses a user whose request is still waiting for an answer', () => {
    const store = createSeedStore()

    expectCode(() => inviteToRoom(store, ME, DEV_ROOM, OLGA), 'not_found')
    expect(store.rooms.get(DEV_ROOM)?.memberIds.has(OLGA)).toBe(false)
  })

  it('refuses a caller who is not in the room', () => {
    const store = createSeedStore()

    expectCode(() => inviteToRoom(store, ME, CHESS_ROOM, ANNA), 'not_found')
    expectCode(() => inviteToRoom(store, ME, 'room-missing', ANNA), 'not_found')
  })

  it('answers already_member for someone in the room, and makes no duplicate', () => {
    const store = createSeedStore()

    expectCode(() => inviteToRoom(store, ME, DEV_ROOM, IGOR), 'already_member')
    inviteToRoom(store, ME, DEV_ROOM, ANNA)
    expectCode(() => inviteToRoom(store, ME, DEV_ROOM, ANNA), 'already_member')

    expect(store.rooms.get(DEV_ROOM)?.memberIds.size).toBe(3)
  })
})

describe('mock room members: events the server would push', () => {
  it('announces an invitation to the room', () => {
    const store = createSeedStore()
    const events = recordEvents(store)

    inviteToRoom(store, ME, DEV_ROOM, ANNA)

    expect(events).toHaveLength(1)
    expect(events[0]).toMatchObject({
      type: 'RoomMemberJoined',
      event: { roomId: DEV_ROOM, user: { id: ANNA } },
    })
  })

  it('announces a join once, and a repeated join not at all', () => {
    const store = createSeedStore()
    const events = recordEvents(store)

    joinRoom(store, ME, CHESS_ROOM)
    joinRoom(store, ME, CHESS_ROOM)

    expect(events).toHaveLength(1)
    expect(events[0]).toMatchObject({ event: { roomId: CHESS_ROOM, user: { id: ME } } })
  })

  it('announces nothing for a refused invitation', () => {
    const store = createSeedStore()
    const events = recordEvents(store)

    expectCode(() => inviteToRoom(store, ME, DEV_ROOM, PETR), 'not_found')

    expect(events).toEqual([])
  })
})
