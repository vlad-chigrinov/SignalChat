import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { api, ApiError, type Message, type Reaction } from '@/api'

import { useMessagesStore } from '../messages'

const ROOM = 'room-1'
const HTTP_TOO_MANY_REQUESTS = 429

const reaction = (emoji: string, count: number, reactedByMe = false): Reaction => ({
  emoji,
  count,
  reactedByMe,
})

function message(id: string, reactions: Reaction[] = []): Message {
  return {
    id,
    conversationId: ROOM,
    author: { id: 'u1', displayName: 'U', avatarUrl: null },
    text: `text ${id}`,
    sentAt: `2026-01-01T10:0${id.slice(1)}:00.000Z`,
    reactions,
  }
}

function storeWith(...messages: Message[]) {
  const store = useMessagesStore()
  for (const item of messages) store.applyIncomingMessage(item)
  return store
}

function reactionsOf(store: ReturnType<typeof useMessagesStore>, id: string): Reaction[] {
  return store.historyOf(ROOM).items.find((item) => item.id === id)?.reactions ?? []
}

describe('reactions in the messages store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  describe('choosing', () => {
    it('shows what the server answers, not a guess', async () => {
      const store = storeWith(message('m1'))
      const choose = vi.spyOn(api, 'chooseReaction').mockResolvedValue([reaction('👍', 1, true)])

      await store.chooseReaction(ROOM, 'm1', '👍')

      expect(choose).toHaveBeenCalledWith(ROOM, 'm1', '👍')
      expect(reactionsOf(store, 'm1')).toEqual([reaction('👍', 1, true)])
    })

    it('leaves the message as it was when the server refuses', async () => {
      const store = storeWith(message('m1', [reaction('👍', 1, true)]))
      vi.spyOn(api, 'chooseReaction').mockRejectedValue(
        new ApiError(HTTP_TOO_MANY_REQUESTS, 'slow down', 'rate_limited'),
      )

      await expect(store.chooseReaction(ROOM, 'm1', '❤️')).rejects.toBeInstanceOf(ApiError)

      expect(reactionsOf(store, 'm1')).toEqual([reaction('👍', 1, true)])
    })

    it('ignores a second click on the same message while the first is on its way', async () => {
      const store = storeWith(message('m1'))
      let finish: (reactions: Reaction[]) => void = () => undefined
      const choose = vi.spyOn(api, 'chooseReaction').mockReturnValue(
        new Promise<Reaction[]>((resolve) => {
          finish = resolve
        }),
      )

      const first = store.chooseReaction(ROOM, 'm1', '👍')
      await store.chooseReaction(ROOM, 'm1', '👍')
      finish([reaction('👍', 1, true)])
      await first

      expect(choose).toHaveBeenCalledTimes(1)
    })

    it('allows the next click once the first has finished, even after a failure', async () => {
      const store = storeWith(message('m1'))
      const choose = vi
        .spyOn(api, 'chooseReaction')
        .mockRejectedValueOnce(new Error('offline'))
        .mockResolvedValueOnce([reaction('👍', 1, true)])

      await expect(store.chooseReaction(ROOM, 'm1', '👍')).rejects.toThrow('offline')
      await store.chooseReaction(ROOM, 'm1', '👍')

      expect(choose).toHaveBeenCalledTimes(2)
      expect(reactionsOf(store, 'm1')).toEqual([reaction('👍', 1, true)])
    })

    it('does not fail when the message is no longer in the list', async () => {
      const store = useMessagesStore()
      vi.spyOn(api, 'chooseReaction').mockResolvedValue([reaction('👍', 1, true)])

      await expect(store.chooseReaction(ROOM, 'gone', '👍')).resolves.toBeUndefined()
    })
  })

  describe('live events', () => {
    it('takes the new counts and keeps my own reaction marked', () => {
      const store = storeWith(message('m1', [reaction('👍', 1, true)]))

      store.applyReactionsChanged({
        conversationId: ROOM,
        messageId: 'm1',
        reactions: [
          { emoji: '👍', count: 2 },
          { emoji: '😂', count: 1 },
        ],
      })

      expect(reactionsOf(store, 'm1')).toEqual([reaction('👍', 2, true), reaction('😂', 1)])
    })

    it('empties the list when the last reaction is removed', () => {
      const store = storeWith(message('m1', [reaction('👍', 1)]))

      store.applyReactionsChanged({ conversationId: ROOM, messageId: 'm1', reactions: [] })

      expect(reactionsOf(store, 'm1')).toEqual([])
    })

    it('marks my reaction made in another tab, and unmarks it when removed there', () => {
      const store = storeWith(message('m1', [reaction('👍', 1), reaction('❤️', 1)]))

      store.applyMyReactionChanged({ conversationId: ROOM, messageId: 'm1', emoji: '❤️' })
      expect(reactionsOf(store, 'm1')).toEqual([reaction('👍', 1), reaction('❤️', 1, true)])

      store.applyMyReactionChanged({ conversationId: ROOM, messageId: 'm1', emoji: null })
      expect(reactionsOf(store, 'm1')).toEqual([reaction('👍', 1), reaction('❤️', 1)])
    })

    it('ignores events for a message that is not loaded', () => {
      const store = storeWith(message('m1'))

      store.applyReactionsChanged({
        conversationId: ROOM,
        messageId: 'm9',
        reactions: [{ emoji: '👍', count: 1 }],
      })
      store.applyMyReactionChanged({ conversationId: 'other', messageId: 'm1', emoji: '👍' })

      expect(reactionsOf(store, 'm1')).toEqual([])
      expect(store.historyOf(ROOM).items).toHaveLength(1)
    })
  })

  describe('history and incoming messages', () => {
    it('keeps the reactions of a message that arrives a second time (send answer, then group event)', () => {
      const store = storeWith(message('m1'))
      store.applyReactionsChanged({
        conversationId: ROOM,
        messageId: 'm1',
        reactions: [{ emoji: '🎉', count: 2 }],
      })

      store.applyIncomingMessage(message('m1'))

      expect(reactionsOf(store, 'm1')).toEqual([reaction('🎉', 2)])
    })

    it('takes the reactions of a re-read page over the old ones', async () => {
      const store = storeWith(message('m1', [reaction('👍', 1)]))
      vi.spyOn(api, 'getMessages').mockResolvedValue({
        items: [message('m1', [reaction('❤️', 3, true)])],
        nextCursor: null,
      })

      await store.refreshLatest(ROOM)

      expect(reactionsOf(store, 'm1')).toEqual([reaction('❤️', 3, true)])
    })
  })
})
