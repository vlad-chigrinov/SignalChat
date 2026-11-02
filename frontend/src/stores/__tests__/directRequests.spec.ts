import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { api, type DirectConversation, type IncomingDirectRequest } from '@/api'
import type { DirectRequestReceivedEvent } from '@/realtime/hubContract'

import { useDirectRequestsStore } from '../directRequests'
import { usePresenceStore } from '../presence'
import { useRoomsStore } from '../rooms'

const OLGA = { id: 'u-olga', displayName: 'Ольга Ким', avatarUrl: null }
const REQUEST: IncomingDirectRequest = {
  id: 'dm-request-olga',
  from: OLGA,
  text: 'Можно обсудить релиз?',
  createdAt: '2026-10-29T10:00:00.000Z',
}

function receivedEvent(id: string, text: string): DirectRequestReceivedEvent {
  return { conversationId: id, from: OLGA, text, createdAt: REQUEST.createdAt }
}

describe('direct requests store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('lists a live request first, and a reopened one replaces its old entry', () => {
    const requests = useDirectRequestsStore()
    requests.incoming = [REQUEST]

    requests.applyReceived(receivedEvent('dm-new', 'Новый запрос'))
    requests.applyReceived(receivedEvent(REQUEST.id, 'Открыли снова'))

    expect(requests.incoming.map((item) => item.id)).toEqual([REQUEST.id, 'dm-new'])
    expect(requests.incoming[0]?.text).toBe('Открыли снова')
    expect(requests.count).toBe(2)
  })

  it('accepting removes the request and opens the chat for the answering user', async () => {
    vi.spyOn(api, 'acceptRequest').mockResolvedValue(undefined)
    const requests = useDirectRequestsStore()
    const rooms = useRoomsStore()
    requests.incoming = [REQUEST]

    await requests.accept(REQUEST.id)

    expect(requests.incoming).toEqual([])
    const chat = rooms.findConversation(REQUEST.id) as DirectConversation
    expect(chat).toMatchObject({ kind: 'direct', status: 'accepted', peer: OLGA })
    expect(chat.lastMessage?.text).toBe(REQUEST.text)
  })

  it('accepting refreshes who is online: the new peer was not in the earlier snapshot', async () => {
    vi.spyOn(api, 'acceptRequest').mockResolvedValue(undefined)
    vi.spyOn(api, 'getOnlineUserIds').mockResolvedValue([OLGA.id])
    const requests = useDirectRequestsStore()
    const presence = usePresenceStore()
    requests.incoming = [REQUEST]
    expect(presence.isOnline(OLGA.id)).toBe(false)

    await requests.accept(REQUEST.id)

    expect(presence.isOnline(OLGA.id)).toBe(true)
  })

  it('declining only removes the request from the list', async () => {
    const declineRequest = vi.spyOn(api, 'declineRequest').mockResolvedValue(undefined)
    const requests = useDirectRequestsStore()
    requests.incoming = [REQUEST]

    await requests.decline(REQUEST.id)

    expect(declineRequest).toHaveBeenCalledWith(REQUEST.id)
    expect(requests.incoming).toEqual([])
  })

  it('keeps the request when the server refuses the answer', async () => {
    vi.spyOn(api, 'acceptRequest').mockRejectedValue(new Error('refused'))
    const requests = useDirectRequestsStore()
    requests.incoming = [REQUEST]

    await expect(requests.accept(REQUEST.id)).rejects.toThrow('refused')
    expect(requests.incoming).toEqual([REQUEST])
  })
})
