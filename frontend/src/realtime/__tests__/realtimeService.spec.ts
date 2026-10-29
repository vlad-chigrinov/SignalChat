import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { api, type Message, type UserSummary } from '@/api'
import { useConnectionStore } from '@/stores/connection'
import { useMessagesStore } from '@/stores/messages'
import { usePresenceStore } from '@/stores/presence'
import { useTypingStore } from '@/stores/typing'

import { HubCallError } from '../chatHub'
import { TYPING_SEND_INTERVAL_MS } from '../hubContract'
import { FakeChatHub } from '../fakeChatHub'
import { createRealtimeService, type RealtimeService } from '../realtimeService'

const ME: UserSummary = { id: 'me', displayName: 'Я', avatarUrl: null }
const PEER: UserSummary = { id: 'peer', displayName: 'Пётр', avatarUrl: null }

function message(id: string, sentAt: string, author: UserSummary = PEER): Message {
  return { id, conversationId: 'room-1', author, text: `text ${id}`, sentAt }
}

describe('realtime service', () => {
  let hub: FakeChatHub
  let service: RealtimeService

  beforeEach(() => {
    vi.useFakeTimers()
    vi.restoreAllMocks()
    setActivePinia(createPinia())
    hub = new FakeChatHub({ currentUser: () => ME })
    service = createRealtimeService(hub)
  })

  afterEach(() => {
    service.dispose()
    vi.useRealTimers()
  })

  describe('events', () => {
    it('applies a received message to the history and clears the author typing', async () => {
      const messages = useMessagesStore()
      const typing = useTypingStore()
      hub.emitTyping({ conversationId: 'room-1', userId: PEER.id })

      hub.emitMessage(message('m1', '2026-01-01T10:00:00.000Z'))

      expect(messages.historyOf('room-1').items.map((m) => m.id)).toEqual(['m1'])
      expect(typing.typingUserIds('room-1')).toEqual([])
    })

    it('applies presence changes and typing events to their stores', async () => {
      const presence = usePresenceStore()
      const typing = useTypingStore()

      hub.emitPresence({ userId: PEER.id, isOnline: true })
      hub.emitTyping({ conversationId: 'room-1', userId: PEER.id })

      expect(presence.isOnline(PEER.id)).toBe(true)
      expect(typing.typingUserIds('room-1')).toEqual([PEER.id])
    })

    it('mirrors the hub connection state into the connection store', async () => {
      const connection = useConnectionStore()

      await service.connect()
      expect(connection.state).toBe('connected')

      hub.simulateReconnect()
      expect(connection.state).toBe('connected')
      expect(connection.isDegraded).toBe(false)
    })
  })

  describe('conversations', () => {
    it('joins on open and leaves on close while connected', async () => {
      await service.connect()

      await service.openConversation('room-1')
      expect(hub.joinedConversations()).toEqual(['room-1'])

      await service.closeConversation('room-1')
      expect(hub.joinedConversations()).toEqual([])
    })

    it('joins the open conversations when the connection starts later', async () => {
      await service.openConversation('room-1')
      expect(hub.joinedConversations()).toEqual([])

      await service.connect()

      expect(hub.joinedConversations()).toEqual(['room-1'])
    })

    it('propagates a hub error from joining and forgets the conversation', async () => {
      await service.connect()
      const joinSpy = vi.spyOn(hub, 'joinRoom').mockRejectedValueOnce(new HubCallError('forbidden'))

      await expect(service.openConversation('room-1')).rejects.toMatchObject({ code: 'forbidden' })
      expect(joinSpy).toHaveBeenCalledWith('room-1')

      // A reconnect must not try to join the refused conversation again.
      vi.spyOn(api, 'getMessages').mockResolvedValue({ items: [], nextCursor: null })
      vi.spyOn(api, 'getOnlineUserIds').mockResolvedValue([])
      joinSpy.mockClear()
      hub.simulateReconnect()
      await vi.runAllTimersAsync()
      expect(joinSpy).not.toHaveBeenCalled()
    })
  })

  describe('reconnect', () => {
    it('rejoins open conversations, refreshes their latest page and reloads presence', async () => {
      await service.connect()
      await service.openConversation('room-1')
      await service.openConversation('dm-1')
      const messages = useMessagesStore()
      messages.applyIncomingMessage(message('old', '2026-01-01T09:00:00.000Z'))

      const getMessages = vi
        .spyOn(api, 'getMessages')
        .mockImplementation(async (conversationId) => ({
          items: [{ ...message('new', '2026-01-01T11:00:00.000Z'), conversationId }],
          nextCursor: null,
        }))
      const getOnline = vi.spyOn(api, 'getOnlineUserIds').mockResolvedValue([PEER.id])
      const joinSpy = vi.spyOn(hub, 'joinRoom')

      hub.simulateReconnect()
      await vi.runAllTimersAsync()

      expect(joinSpy.mock.calls.map(([id]) => id).sort()).toEqual(['dm-1', 'room-1'])
      expect(getMessages).toHaveBeenCalledWith('room-1', {
        before: null,
        limit: expect.any(Number),
      })
      expect(getMessages).toHaveBeenCalledWith('dm-1', { before: null, limit: expect.any(Number) })
      expect(getOnline).toHaveBeenCalledTimes(1)
      expect(usePresenceStore().isOnline(PEER.id)).toBe(true)
      expect(messages.historyOf('room-1').items.map((m) => m.id)).toEqual(['old', 'new'])
    })
  })

  describe('sending', () => {
    it('sends through the hub when connected and keeps one copy of the echoed message', async () => {
      await service.connect()
      const messages = useMessagesStore()
      const restSend = vi.spyOn(api, 'sendMessage')

      const sent = await service.sendMessage('room-1', '  привет  ')

      expect(sent.text).toBe('привет')
      expect(restSend).not.toHaveBeenCalled()
      expect(messages.historyOf('room-1').items).toHaveLength(1)
    })

    it('falls back to REST when the connection is down', async () => {
      const sentByRest = message('rest-1', '2026-01-01T10:00:00.000Z', ME)
      const restSend = vi.spyOn(api, 'sendMessage').mockResolvedValue(sentByRest)

      const sent = await service.sendMessage('room-1', 'текст')

      expect(restSend).toHaveBeenCalledWith('room-1', 'текст')
      expect(sent).toEqual(sentByRest)
      expect(useMessagesStore().historyOf('room-1').items).toEqual([sentByRest])
    })

    it('propagates hub errors as HubCallError', async () => {
      // Signed out: the hub refuses the send with `unauthenticated`.
      const signedOutHub = new FakeChatHub({ currentUser: () => null })
      const signedOutService = createRealtimeService(signedOutHub)
      await signedOutService.connect()

      await expect(signedOutService.sendMessage('room-1', 'x')).rejects.toMatchObject({
        code: 'unauthenticated',
      })
      signedOutService.dispose()
    })

    it('rejects an empty message before any call', async () => {
      await service.connect()
      const restSend = vi.spyOn(api, 'sendMessage')

      await expect(service.sendMessage('room-1', '   ')).rejects.toBeInstanceOf(RangeError)
      expect(restSend).not.toHaveBeenCalled()
    })
  })

  describe('typing', () => {
    it('sends typing at most once per interval per conversation', async () => {
      await service.connect()

      service.notifyTyping('room-1')
      service.notifyTyping('room-1')
      service.notifyTyping('dm-1')
      expect(hub.typingCalls()).toEqual(['room-1', 'dm-1'])

      vi.advanceTimersByTime(TYPING_SEND_INTERVAL_MS)
      service.notifyTyping('room-1')
      expect(hub.typingCalls()).toEqual(['room-1', 'dm-1', 'room-1'])
    })

    it('sends nothing while disconnected', () => {
      service.notifyTyping('room-1')

      expect(hub.typingCalls()).toEqual([])
    })

    it('clears the typing state of a conversation when it is closed', async () => {
      await service.connect()
      await service.openConversation('room-1')
      hub.emitTyping({ conversationId: 'room-1', userId: PEER.id })

      await service.closeConversation('room-1')

      expect(useTypingStore().typingUserIds('room-1')).toEqual([])
    })
  })
})
