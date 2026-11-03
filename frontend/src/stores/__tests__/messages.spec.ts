import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { api, type Message } from '@/api'

import { MESSAGE_PAGE_SIZE, useMessagesStore } from '../messages'

function message(index: number): Message {
  return {
    id: `m${index}`,
    conversationId: 'room-1',
    author: { id: 'u1', displayName: 'U', avatarUrl: null },
    text: `text ${index}`,
    sentAt: new Date(Date.UTC(2026, 0, 1, 10, index)).toISOString(),
    reactions: [],
  }
}

describe('messages store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('loads the latest page, then the older one with the cursor', async () => {
    const getMessages = vi
      .spyOn(api, 'getMessages')
      .mockResolvedValueOnce({ items: [message(3), message(4)], nextCursor: 'c-older' })
      .mockResolvedValueOnce({ items: [message(1), message(2)], nextCursor: null })
    const store = useMessagesStore()

    await store.loadLatest('room-1')
    await store.loadOlder('room-1')
    await store.loadOlder('room-1') // no cursor left: no request

    expect(getMessages).toHaveBeenNthCalledWith(1, 'room-1', {
      before: null,
      limit: MESSAGE_PAGE_SIZE,
    })
    expect(getMessages).toHaveBeenNthCalledWith(2, 'room-1', {
      before: 'c-older',
      limit: MESSAGE_PAGE_SIZE,
    })
    expect(getMessages).toHaveBeenCalledTimes(2)
    expect(store.historyOf('room-1').items.map((m) => m.id)).toEqual(['m1', 'm2', 'm3', 'm4'])
  })

  it('applies an incoming real-time message once', () => {
    const store = useMessagesStore()
    store.applyIncomingMessage(message(1))
    store.applyIncomingMessage(message(1))
    expect(store.historyOf('room-1').items).toHaveLength(1)
  })

  it('keeps the error on a failed page and stays retryable', async () => {
    vi.spyOn(api, 'getMessages').mockRejectedValueOnce(new Error('offline'))
    const store = useMessagesStore()

    await store.loadLatest('room-1')

    const history = store.historyOf('room-1')
    expect(history.error).not.toBeNull()
    expect(history.hasLoadedLatest).toBe(false)
    expect(history.isLoading).toBe(false)
  })
})
