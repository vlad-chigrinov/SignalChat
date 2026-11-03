import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import type { Message, Reaction, UserSummary } from '@/api'
import { mockCurrentUser } from '@/api/mock/mockData'
import { resetSharedMockStore } from '@/api/mock/mockHistories'
import { useMessagesStore } from '@/stores/messages'

import { FakeChatHub } from '../fakeChatHub'
import { createRealtimeService, type RealtimeService } from '../realtimeService'

const ME: UserSummary = {
  id: mockCurrentUser.id,
  displayName: mockCurrentUser.displayName,
  avatarUrl: mockCurrentUser.avatarUrl,
}
const PEER: UserSummary = { id: 'peer', displayName: 'Пётр', avatarUrl: null }
const ROOM = 'room-general'

function message(): Message {
  return {
    id: 'm1',
    conversationId: ROOM,
    author: PEER,
    text: 'привет',
    sentAt: '2026-01-01T10:00:00.000Z',
    attachments: [],
    reactions: [],
  }
}

describe('realtime reactions', () => {
  let hub: FakeChatHub
  let service: RealtimeService

  beforeEach(() => {
    resetSharedMockStore()
    setActivePinia(createPinia())
    hub = new FakeChatHub({ currentUser: () => ME })
    service = createRealtimeService(hub)
    useMessagesStore().applyIncomingMessage(message())
  })

  afterEach(() => {
    service.dispose()
  })

  function reactions(): Reaction[] {
    return useMessagesStore().historyOf(ROOM).items[0]?.reactions ?? []
  }

  it('shows the counts of another member’s reaction without a reload', () => {
    hub.emitReactionsChanged({
      conversationId: ROOM,
      messageId: 'm1',
      reactions: [{ emoji: '👍', count: 1 }],
    })

    expect(reactions()).toEqual([{ emoji: '👍', count: 1, reactedByMe: false }])
  })

  it('updates the counts as members change their minds', () => {
    hub.emitReactionsChanged({
      conversationId: ROOM,
      messageId: 'm1',
      reactions: [{ emoji: '👍', count: 2 }],
    })
    hub.emitReactionsChanged({
      conversationId: ROOM,
      messageId: 'm1',
      reactions: [
        { emoji: '👍', count: 1 },
        { emoji: '🎉', count: 1 },
      ],
    })

    expect(reactions().map((reaction) => [reaction.emoji, reaction.count])).toEqual([
      ['👍', 1],
      ['🎉', 1],
    ])
  })

  it('marks my reaction made in another tab: the counts first, then the mark', () => {
    hub.emitReactionsChanged({
      conversationId: ROOM,
      messageId: 'm1',
      reactions: [{ emoji: '❤️', count: 1 }],
    })
    hub.emitMyReactionChanged({ conversationId: ROOM, messageId: 'm1', emoji: '❤️' })

    expect(reactions()).toEqual([{ emoji: '❤️', count: 1, reactedByMe: true }])
  })

  it('stops reacting to events after dispose', () => {
    service.dispose()

    hub.emitReactionsChanged({
      conversationId: ROOM,
      messageId: 'm1',
      reactions: [{ emoji: '👍', count: 1 }],
    })

    expect(reactions()).toEqual([])
  })
})
