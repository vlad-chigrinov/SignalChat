import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import type { DirectConversation, Message, UserSummary } from '@/api'

import { useAuthStore } from '../auth'
import { useDirectRequestsStore } from '../directRequests'
import { useMessagesStore } from '../messages'
import { useRoomMembersStore } from '../roomMembers'
import { useRoomNoticesStore } from '../roomNotices'
import { applyProfileChange } from '../profileSync'
import { useRoomsStore } from '../rooms'
import { useUserSearchStore } from '../userSearch'

const ANNA_BEFORE: UserSummary = { id: 'u-anna', displayName: 'Анна', avatarUrl: null }
const ANNA_AFTER: UserSummary = {
  id: 'u-anna',
  displayName: 'Анна Новая',
  avatarUrl: '/api/users/u-anna/avatar?v=1',
}
const IGOR: UserSummary = { id: 'u-igor', displayName: 'Игорь', avatarUrl: null }

function messageBy(id: string, author: UserSummary): Message {
  return {
    id,
    conversationId: 'room-1',
    author,
    text: 'привет',
    sentAt: '2026-11-02T10:00:00.000Z',
  }
}

describe('applying a profile change everywhere the user is shown', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('renames the peer of a direct conversation, and leaves other peers alone', () => {
    const rooms = useRoomsStore()
    const withAnna: DirectConversation = {
      kind: 'direct',
      id: 'dm-anna',
      peer: ANNA_BEFORE,
      status: 'accepted',
      lastMessage: null,
    }
    rooms.directConversations = [withAnna, { ...withAnna, id: 'dm-igor', peer: IGOR }]

    applyProfileChange(ANNA_AFTER)

    expect(rooms.directConversations.map((direct) => direct.peer)).toEqual([ANNA_AFTER, IGOR])
  })

  it('updates the sender of an incoming request', () => {
    const requests = useDirectRequestsStore()
    requests.incoming = [
      {
        id: 'dm-request',
        from: ANNA_BEFORE,
        text: 'Можно?',
        createdAt: '2026-11-02T09:00:00.000Z',
      },
    ]

    applyProfileChange(ANNA_AFTER)

    expect(requests.incoming[0]?.from).toEqual(ANNA_AFTER)
  })

  it('updates the author of loaded messages in every conversation', () => {
    const messages = useMessagesStore()
    messages.histories['room-1'] = {
      items: [messageBy('m1', ANNA_BEFORE), messageBy('m2', IGOR)],
      nextCursor: null,
      hasLoadedLatest: true,
      isLoading: false,
      error: null,
    }
    messages.histories['dm-1'] = {
      items: [{ ...messageBy('m3', ANNA_BEFORE), conversationId: 'dm-1' }],
      nextCursor: null,
      hasLoadedLatest: true,
      isLoading: false,
      error: null,
    }

    applyProfileChange(ANNA_AFTER)

    expect(messages.historyOf('room-1').items.map((item) => item.author)).toEqual([
      ANNA_AFTER,
      IGOR,
    ])
    expect(messages.historyOf('dm-1').items[0]?.author).toEqual(ANNA_AFTER)
  })

  it('updates the user search results', () => {
    const search = useUserSearchStore()
    search.results = [ANNA_BEFORE, IGOR]

    applyProfileChange(ANNA_AFTER)

    expect(search.results).toEqual([ANNA_AFTER, IGOR])
  })

  it('does nothing when the user is not shown anywhere', () => {
    const rooms = useRoomsStore()

    applyProfileChange(ANNA_AFTER)

    expect(rooms.directConversations).toEqual([])
  })
})

describe('auth store own profile', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('renames a room member and a "joined" notice, and leaves others alone', () => {
    const members = useRoomMembersStore()
    members.byRoom['room-1'] = {
      status: 'ready',
      error: null,
      items: [
        { user: ANNA_BEFORE, role: 'owner' },
        { user: IGOR, role: 'member' },
      ],
    }
    const notices = useRoomNoticesStore()
    notices.applyMemberJoined({
      roomId: 'room-1',
      user: ANNA_BEFORE,
      joinedAt: '2026-11-02T10:00:00.000Z',
    })

    applyProfileChange(ANNA_AFTER)

    expect(members.entryOf('room-1').items.map((member) => member.user.displayName)).toEqual([
      'Анна Новая',
      'Игорь',
    ])
    expect(notices.noticesOf('room-1')[0]?.user).toEqual(ANNA_AFTER)
  })

  it('takes the new name and avatar when the change is about the signed-in user', () => {
    const auth = useAuthStore()
    auth.user = { id: 'u-me', displayName: 'Я', avatarUrl: null, email: 'me@example.com' }

    auth.applyOwnProfile({ id: 'u-me', displayName: 'Я с другой вкладки', avatarUrl: '/x?v=1' })

    expect(auth.user).toEqual({
      id: 'u-me',
      displayName: 'Я с другой вкладки',
      avatarUrl: '/x?v=1',
      email: 'me@example.com',
    })
  })

  it('ignores a change of someone else', () => {
    const auth = useAuthStore()
    auth.user = { id: 'u-me', displayName: 'Я', avatarUrl: null, email: 'me@example.com' }

    auth.applyOwnProfile(ANNA_AFTER)

    expect(auth.user.displayName).toBe('Я')
  })
})
