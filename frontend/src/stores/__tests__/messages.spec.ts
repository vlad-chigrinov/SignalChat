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
    attachments: [],
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

  it('sends photos through the API and puts the answer in the history and the list preview', async () => {
    const sent: Message = {
      ...message(7),
      text: 'закат',
      attachments: [
        {
          id: 'a1',
          width: 4,
          height: 3,
          thumbnailWidth: 4,
          thumbnailHeight: 3,
          sizeBytes: 3,
          contentType: 'image/webp',
          fileName: 'a.webp',
          url: '/api/attachments/a1/original',
          thumbnailUrl: '/api/attachments/a1/thumbnail',
        },
      ],
    }
    const sendPhotoMessage = vi.spyOn(api, 'sendPhotoMessage').mockResolvedValue(sent)
    const store = useMessagesStore()
    const photos = [new File([new Uint8Array([1])], 'a.png', { type: 'image/png' })]

    const result = await store.sendPhotos('room-1', 'закат', photos)

    expect(sendPhotoMessage).toHaveBeenCalledWith('room-1', 'закат', photos)
    expect(result).toBe(sent)
    expect(store.historyOf('room-1').items).toEqual([sent])
  })

  it('keeps one copy when the hub delivers the photo message that REST already returned', async () => {
    const sent: Message = { ...message(8), attachments: [] }
    vi.spyOn(api, 'sendPhotoMessage').mockResolvedValue(sent)
    const store = useMessagesStore()

    await store.sendPhotos('room-1', '', [])
    store.applyIncomingMessage(sent)

    expect(store.historyOf('room-1').items).toHaveLength(1)
  })

  it('does not touch the history when the server refuses the photos', async () => {
    vi.spyOn(api, 'sendPhotoMessage').mockRejectedValue(new Error('refused'))
    const store = useMessagesStore()

    await expect(store.sendPhotos('room-1', '', [])).rejects.toThrow('refused')

    expect(store.historyOf('room-1').items).toEqual([])
  })
})
