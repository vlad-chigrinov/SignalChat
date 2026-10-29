import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { api, type Message } from '@/api'

import { useMessagesStore } from '../messages'

function message(index: number): Message {
  return {
    id: `m${index}`,
    conversationId: 'room-1',
    author: { id: 'u1', displayName: 'U', avatarUrl: null },
    text: `text ${index}`,
    sentAt: new Date(Date.UTC(2026, 0, 1, 10, index)).toISOString(),
  }
}

describe('messages store: refreshLatest', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('merges the newest page and keeps the older history and its cursor', async () => {
    const store = useMessagesStore()
    vi.spyOn(api, 'getMessages')
      .mockResolvedValueOnce({ items: [message(3), message(4)], nextCursor: 'c-older' })
      .mockResolvedValueOnce({ items: [message(1), message(2)], nextCursor: null })
    await store.loadLatest('room-1')
    await store.loadOlder('room-1')
    vi.spyOn(api, 'getMessages').mockResolvedValueOnce({
      items: [message(4), message(5)],
      nextCursor: 'c-latest-older',
    })

    await store.refreshLatest('room-1')

    const history = store.historyOf('room-1')
    expect(history.items.map((m) => m.id)).toEqual(['m1', 'm2', 'm3', 'm4', 'm5'])
    expect(history.nextCursor).toBeNull()
  })

  it('takes the cursor from the first load when there was no history yet', async () => {
    const store = useMessagesStore()
    vi.spyOn(api, 'getMessages').mockResolvedValueOnce({
      items: [message(4)],
      nextCursor: 'c-older',
    })

    await store.refreshLatest('room-1')

    expect(store.historyOf('room-1').nextCursor).toBe('c-older')
    expect(store.historyOf('room-1').hasLoadedLatest).toBe(true)
  })

  it('leaves the history untouched when the refresh fails', async () => {
    const store = useMessagesStore()
    store.applyIncomingMessage(message(1))
    vi.spyOn(api, 'getMessages').mockRejectedValueOnce(new Error('offline'))

    await store.refreshLatest('room-1')

    expect(store.historyOf('room-1').items.map((m) => m.id)).toEqual(['m1'])
    expect(store.historyOf('room-1').error).toBeNull()
  })
})
