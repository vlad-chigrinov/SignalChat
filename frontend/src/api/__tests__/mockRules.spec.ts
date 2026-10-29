import { describe, expect, it } from 'vitest'

import { ApiError } from '../http'
import { createSeedStore, mockCurrentUser } from '../mock/mockData'
import { requireMember } from '../mock/mockHistories'
import { createRoom, discoverRooms, joinRoom, leaveRoom, listMyRooms } from '../mock/mockRooms'
import { openDirectConversation, listMyDirects } from '../mock/mockDirects'
import { searchUsers } from '../mock/mockUsers'

const ME = mockCurrentUser.id
const ANNA = 'u-anna'
const OLGA = 'u-olga'
const GENERAL = 'room-general'
const CHESS = 'room-chess'
const QUIET = 'room-quiet'
const DEV = 'room-dev'
const LONG_NAME_LENGTH = 81
const MAX_DESCRIPTION_LENGTH = 300
const OWNED_ROOM_LIMIT = 20

/** Asserts that the rule throws an ApiError with the given stable code. */
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

describe('mock rooms', () => {
  it('lists only the rooms the caller is in, with the member count', () => {
    const store = createSeedStore()

    const rooms = listMyRooms(store, ME)

    expect(rooms.map((room) => room.id)).toEqual([GENERAL, DEV, QUIET])
    expect(rooms.find((room) => room.id === GENERAL)?.memberCount).toBe(4)
  })

  it('discovers rooms the caller is not in, with no preview', () => {
    const store = createSeedStore()

    const found = discoverRooms(store, ME, '', 10)

    expect(found.map((room) => room.id)).toEqual([CHESS])
    expect(found[0]?.lastMessage).toBeNull()
  })

  it('matches discovery by name, ignoring case and surrounding spaces', () => {
    const store = createSeedStore()

    expect(discoverRooms(store, ME, '  ШАХ ', 10).map((room) => room.id)).toEqual([CHESS])
    expect(discoverRooms(store, ME, 'нет такой', 10)).toEqual([])
  })

  it('creates a room owned by the caller, with trimmed name and description', () => {
    const store = createSeedStore()

    const room = createRoom(store, ME, { name: '  Новая  ', description: '  текст  ' })

    expect(room).toMatchObject({ name: 'Новая', description: 'текст', memberCount: 1 })
    expect(listMyRooms(store, ME).map((item) => item.id)).toContain(room.id)
  })

  it('rejects a room name shorter than 3 or longer than 80 characters after trim', () => {
    const store = createSeedStore()

    expectCode(
      () => createRoom(store, ME, { name: '  ab  ', description: '' }),
      'invalid_room_name',
    )
    expectCode(
      () => createRoom(store, ME, { name: 'а'.repeat(LONG_NAME_LENGTH), description: '' }),
      'invalid_room_name',
    )
  })

  it('rejects a description longer than 300 characters', () => {
    const store = createSeedStore()

    expectCode(
      () =>
        createRoom(store, ME, {
          name: 'Нормальное имя',
          description: 'а'.repeat(MAX_DESCRIPTION_LENGTH + 1),
        }),
      'invalid_room_description',
    )
  })

  it('rejects a name taken in any case by another room', () => {
    const store = createSeedStore()

    expectCode(() => createRoom(store, ME, { name: 'ОБЩИЙ', description: '' }), 'room_name_taken')
  })

  it('stops the caller at 20 owned rooms', () => {
    const store = createSeedStore()
    // The seed already gives the caller one owned room (QUIET).
    for (let index = 1; index < OWNED_ROOM_LIMIT; index += 1) {
      createRoom(store, ME, { name: `Комната ${index}`, description: '' })
    }

    expectCode(
      () => createRoom(store, ME, { name: 'Лишняя', description: '' }),
      'room_limit_reached',
    )
  })

  it('joins are idempotent and a missing room answers not_found', () => {
    const store = createSeedStore()

    joinRoom(store, ME, CHESS)
    joinRoom(store, ME, CHESS)

    expect(listMyRooms(store, ME).filter((room) => room.id === CHESS)).toHaveLength(1)
    expectCode(() => joinRoom(store, ME, 'room-missing'), 'not_found')
  })

  it('refuses an owner leaving while other members remain', () => {
    const store = createSeedStore()

    // Anna owns GENERAL with three members: she cannot leave it.
    expectCode(() => leaveRoom(store, ANNA, GENERAL), 'owner_cannot_leave')
  })

  it('lets a plain member leave, and deletes the room when the last member leaves', () => {
    const store = createSeedStore()

    // Igor owns DEV, so the caller (a plain member there) is the one who leaves.
    leaveRoom(store, ME, DEV)
    expect(listMyRooms(store, ME).map((room) => room.id)).not.toContain(DEV)

    // The caller is the only member left in QUIET: leaving deletes the room.
    leaveRoom(store, ME, QUIET)
    expect(store.rooms.has(QUIET)).toBe(false)
    expect(store.histories.has(QUIET)).toBe(false)
  })

  it('answers not_found when a non-member tries to leave', () => {
    const store = createSeedStore()

    expectCode(() => leaveRoom(store, ME, CHESS), 'not_found')
  })
})

describe('mock direct conversations', () => {
  it('lists only the direct conversations the caller is in', () => {
    const store = createSeedStore()

    expect(listMyDirects(store, ME).map((direct) => direct.id)).toEqual(['dm-anna', 'dm-igor'])
  })

  it('opens a new conversation with a user, then reuses it', () => {
    const store = createSeedStore()

    // Olga has no conversation with the caller in the seed, so the first call creates it.
    const first = openDirectConversation(store, ME, OLGA)
    const second = openDirectConversation(store, ME, OLGA)

    expect(first.peer.id).toBe(OLGA)
    expect(second.id).toBe(first.id)
    expect(listMyDirects(store, ME).filter((direct) => direct.id === first.id)).toHaveLength(1)
  })

  it('rejects a conversation with oneself and an unknown user', () => {
    const store = createSeedStore()

    expectCode(() => openDirectConversation(store, ME, ME), 'self_conversation')
    expectCode(() => openDirectConversation(store, ME, 'u-nobody'), 'not_found')
  })
})

describe('mock user search', () => {
  it('needs at least two characters', () => {
    const store = createSeedStore()

    expectCode(() => searchUsers(store, ME, ' А ', 10), 'query_too_short')
  })

  it('never returns the caller, even when the name matches', () => {
    const store = createSeedStore()

    // The caller's own display name is 'Вы (демо)'.
    expect(searchUsers(store, ME, 'де', 10)).toEqual([])
  })

  it('returns users without an email field', () => {
    const store = createSeedStore()

    const found = searchUsers(store, ME, 'ан', 10)

    expect(found.map((user) => user.id)).toEqual([ANNA])
    expect(found.every((user) => !('email' in user))).toBe(true)
  })

  it('caps the number of results at the given limit', () => {
    const store = createSeedStore()

    // 'ов' matches Петров and Смирнова.
    expect(searchUsers(store, ME, 'ов', 10)).toHaveLength(2)
    expect(searchUsers(store, ME, 'ов', 1)).toHaveLength(1)
  })
})

describe('mock history access', () => {
  it('answers not_found for a conversation the caller is not in', () => {
    const store = createSeedStore()

    expect(() => requireMember(store, CHESS, ME)).toThrow(ApiError)
    expect(() => requireMember(store, GENERAL, ME)).not.toThrow()
  })
})
