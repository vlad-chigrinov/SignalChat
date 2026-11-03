import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import type { UserSummary } from '@/api'
import type { RoomMemberJoinedEvent } from '@/realtime/hubContract'

import { MAX_NOTICES_PER_ROOM, useRoomNoticesStore } from '../roomNotices'

const ROOM_ID = 'room-dev'
const ANNA: UserSummary = { id: 'u-anna', displayName: 'Анна', avatarUrl: null }
const IGOR: UserSummary = { id: 'u-igor', displayName: 'Игорь', avatarUrl: null }

function joined(user: UserSummary, joinedAt: string, roomId = ROOM_ID): RoomMemberJoinedEvent {
  return { roomId, user, joinedAt }
}

describe('room notices store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('has no notices for a room nobody joined', () => {
    expect(useRoomNoticesStore().noticesOf(ROOM_ID)).toEqual([])
  })

  it('records a join as a notice of its room, in the order they came', () => {
    const store = useRoomNoticesStore()

    store.applyMemberJoined(joined(ANNA, '2026-11-02T10:00:00.000Z'))
    store.applyMemberJoined(joined(IGOR, '2026-11-02T10:05:00.000Z'))
    store.applyMemberJoined(joined(IGOR, '2026-11-02T10:06:00.000Z', 'room-other'))

    const notices = store.noticesOf(ROOM_ID)
    expect(notices.map((notice) => notice.user.id)).toEqual([ANNA.id, IGOR.id])
    expect(notices[0]).toMatchObject({ kind: 'member-joined', at: '2026-11-02T10:00:00.000Z' })
    expect(store.noticesOf('room-other')).toHaveLength(1)
  })

  it('records the same join only once', () => {
    const store = useRoomNoticesStore()

    store.applyMemberJoined(joined(ANNA, '2026-11-02T10:00:00.000Z'))
    store.applyMemberJoined(joined(ANNA, '2026-11-02T10:00:00.000Z'))

    expect(store.noticesOf(ROOM_ID)).toHaveLength(1)
  })

  it('records a second join of the same person at another time (left, then came back)', () => {
    const store = useRoomNoticesStore()

    store.applyMemberJoined(joined(ANNA, '2026-11-02T10:00:00.000Z'))
    store.applyMemberJoined(joined(ANNA, '2026-11-02T11:00:00.000Z'))

    expect(store.noticesOf(ROOM_ID)).toHaveLength(2)
  })

  it('keeps only the newest notices of a room', () => {
    const store = useRoomNoticesStore()

    for (let index = 0; index < MAX_NOTICES_PER_ROOM + 5; index += 1) {
      const user = { id: `u-${index}`, displayName: `Гость ${index}`, avatarUrl: null }
      store.applyMemberJoined(
        joined(user, new Date(Date.UTC(2026, 10, 2, 10, index)).toISOString()),
      )
    }

    const notices = store.noticesOf(ROOM_ID)
    expect(notices).toHaveLength(MAX_NOTICES_PER_ROOM)
    expect(notices.at(-1)?.user.id).toBe(`u-${MAX_NOTICES_PER_ROOM + 4}`)
    expect(notices[0]?.user.id).toBe('u-5')
  })

  it('shows a changed name in the notices', () => {
    const store = useRoomNoticesStore()
    store.applyMemberJoined(joined(ANNA, '2026-11-02T10:00:00.000Z'))

    store.applyProfileChange({ ...ANNA, displayName: 'Анна Новая' })

    expect(store.noticesOf(ROOM_ID)[0]?.user.displayName).toBe('Анна Новая')
  })

  it('forgets everything on reset', () => {
    const store = useRoomNoticesStore()
    store.applyMemberJoined(joined(ANNA, '2026-11-02T10:00:00.000Z'))

    store.reset()

    expect(store.noticesOf(ROOM_ID)).toEqual([])
  })
})
