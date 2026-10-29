import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { api, type Message, type UserSummary } from '@/api'
import { MAX_MESSAGE_LENGTH } from '@/utils/messageText'
import { useConnectionStore } from '@/stores/connection'
import { useMessagesStore } from '@/stores/messages'
import { usePresenceStore } from '@/stores/presence'
import { useTypingStore } from '@/stores/typing'

import { HubCallError, NotConnectedError } from '../chatHub'
import { FakeChatHub } from '../fakeChatHub'
import { TYPING_SEND_INTERVAL_MS } from '../hubContract'
import { createRealtimeService, type RealtimeService } from '../realtimeService'

const ME: UserSummary = { id: 'me', displayName: 'Я', avatarUrl: null }
const PEER: UserSummary = { id: 'peer', displayName: 'Пётр', avatarUrl: null }
const ROOM = 'room-general'
const DM = 'dm-anna'
/** Enough microtask turns for a chained promise (`getCurrentUser().catch(...)`) to settle. */
const MICROTASK_TURNS = 5

function message(id: string, sentAt: string, author: UserSummary = PEER): Message {
  return { id, conversationId: ROOM, author, text: `text ${id}`, sentAt }
}

async function flushPromises(): Promise<void> {
  for (let turn = 0; turn < MICROTASK_TURNS; turn += 1) await Promise.resolve()
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
      hub.emitTyping({ conversationId: ROOM, userId: PEER.id })

      hub.emitMessage(message('m1', '2026-01-01T10:00:00.000Z'))

      expect(messages.historyOf(ROOM).items.map((m) => m.id)).toEqual(['m1'])
      expect(typing.typingUserIds(ROOM)).toEqual([])
    })

    it('applies presence changes and typing events to their stores', async () => {
      const presence = usePresenceStore()
      const typing = useTypingStore()

      hub.emitPresence({ userId: PEER.id, isOnline: true })
      hub.emitTyping({ conversationId: ROOM, userId: PEER.id })

      expect(presence.isOnline(PEER.id)).toBe(true)
      expect(typing.typingUserIds(ROOM)).toEqual([PEER.id])
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

      await service.openConversation(ROOM)
      expect(hub.joinedConversations()).toEqual([ROOM])

      await service.closeConversation(ROOM)
      expect(hub.joinedConversations()).toEqual([])
    })

    it('joins the open conversations when the connection starts later', async () => {
      await service.openConversation(ROOM)
      expect(hub.joinedConversations()).toEqual([])

      await service.connect()

      expect(hub.joinedConversations()).toEqual([ROOM])
    })

    it('propagates a hub error from joining and forgets the conversation', async () => {
      await service.connect()
      const joinSpy = vi.spyOn(hub, 'joinRoom').mockRejectedValueOnce(new HubCallError('not_found'))

      await expect(service.openConversation(ROOM)).rejects.toMatchObject({ code: 'not_found' })
      expect(joinSpy).toHaveBeenCalledWith(ROOM)

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
      await service.openConversation(ROOM)
      await service.openConversation(DM)
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

      expect(joinSpy.mock.calls.map(([id]) => id).sort()).toEqual([DM, ROOM])
      expect(getMessages).toHaveBeenCalledWith(ROOM, { before: null, limit: expect.any(Number) })
      expect(getMessages).toHaveBeenCalledWith(DM, { before: null, limit: expect.any(Number) })
      expect(getOnline).toHaveBeenCalledTimes(1)
      expect(usePresenceStore().isOnline(PEER.id)).toBe(true)
      expect(messages.historyOf(ROOM).items.map((m) => m.id)).toEqual(['old', 'new'])
    })
  })

  describe('sending (hub only, C1)', () => {
    it('sends through the hub and keeps one copy of the echoed message', async () => {
      await service.connect()
      const messages = useMessagesStore()
      const restSend = vi.spyOn(api, 'getMessages')

      const sent = await service.sendMessage(ROOM, '  привет  ')

      expect(sent.text).toBe('привет')
      expect(restSend).not.toHaveBeenCalled()
      expect(messages.historyOf(ROOM).items).toHaveLength(1)
    })

    it('refuses to send without a live connection, with NotConnectedError and no other call', async () => {
      const messages = useMessagesStore()
      const hubSend = vi.spyOn(hub, 'sendMessage')

      await expect(service.sendMessage(ROOM, 'текст')).rejects.toBeInstanceOf(NotConnectedError)
      expect(hubSend).not.toHaveBeenCalled()
      expect(messages.historyOf(ROOM).items).toEqual([])
    })

    it('propagates hub errors as HubCallError', async () => {
      // Signed out: the hub refuses the send with `unauthenticated`.
      const signedOutHub = new FakeChatHub({ currentUser: () => null })
      const signedOutService = createRealtimeService(signedOutHub)
      await signedOutService.connect()

      await expect(signedOutService.sendMessage(ROOM, 'x')).rejects.toMatchObject({
        code: 'unauthenticated',
      })
      signedOutService.dispose()
    })

    it('rejects an empty or oversized message before any call', async () => {
      await service.connect()
      const hubSend = vi.spyOn(hub, 'sendMessage')

      await expect(service.sendMessage(ROOM, '   ')).rejects.toBeInstanceOf(RangeError)
      await expect(service.sendMessage(ROOM, 'x'.repeat(MAX_MESSAGE_LENGTH + 1))).rejects.toThrow(
        RangeError,
      )
      expect(hubSend).not.toHaveBeenCalled()
    })
  })

  describe('typing', () => {
    it('sends typing at most once per interval per conversation', async () => {
      await service.connect()

      service.notifyTyping(ROOM)
      service.notifyTyping(ROOM)
      service.notifyTyping(DM)
      expect(hub.typingCalls()).toEqual([ROOM, DM])

      vi.advanceTimersByTime(TYPING_SEND_INTERVAL_MS)
      service.notifyTyping(ROOM)
      expect(hub.typingCalls()).toEqual([ROOM, DM, ROOM])
    })

    it('sends nothing while disconnected', () => {
      service.notifyTyping(ROOM)

      expect(hub.typingCalls()).toEqual([])
    })

    it('clears the typing state of a conversation when it is closed', async () => {
      await service.connect()
      await service.openConversation(ROOM)
      hub.emitTyping({ conversationId: ROOM, userId: PEER.id })

      await service.closeConversation(ROOM)

      expect(useTypingStore().typingUserIds(ROOM)).toEqual([])
    })
  })

  describe('start retries', () => {
    it('retries a failed start until it succeeds, then joins the open conversations', async () => {
      hub.failNextStarts([new HubCallError('unknown'), new HubCallError('unknown')])
      await service.openConversation(ROOM)

      const started = service.connect()
      await vi.runAllTimersAsync()
      await started

      expect(hub.startAttempts()).toBe(3)
      expect(hub.currentState()).toBe('connected')
      expect(hub.joinedConversations()).toEqual([ROOM])
    })

    it('does not retry a 401 on start: the session is over', async () => {
      const connection = useConnectionStore()
      hub.failNextStarts([new HubCallError('unauthenticated')])

      await expect(service.connect()).rejects.toMatchObject({ code: 'unauthenticated' })
      await vi.runAllTimersAsync()

      expect(hub.startAttempts()).toBe(1)
      expect(connection.sessionEnded).toBe(true)
    })

    it('stops retrying once disconnect() is called', async () => {
      hub.failNextStarts(Array.from({ length: 10 }, () => new HubCallError('unknown')))

      const started = service.connect()
      await service.disconnect()
      await vi.runAllTimersAsync()
      await started

      expect(hub.startAttempts()).toBe(1)
      expect(hub.currentState()).toBe('disconnected')
    })
  })

  describe('server-side close', () => {
    it('ends the session when the server closes the connection and the session is rejected', async () => {
      const connection = useConnectionStore()
      vi.spyOn(api, 'getCurrentUser').mockResolvedValue(null)
      await service.connect()

      hub.simulateServerClose()
      await flushPromises()

      expect(connection.state).toBe('disconnected')
      expect(connection.sessionEnded).toBe(true)
    })

    it('keeps the session when the close came from a network loss', async () => {
      const connection = useConnectionStore()
      vi.spyOn(api, 'getCurrentUser').mockRejectedValue(new Error('offline'))
      await service.connect()

      hub.simulateServerClose()
      await flushPromises()

      expect(connection.sessionEnded).toBe(false)
    })

    it('does not treat our own disconnect as a server close', async () => {
      const connection = useConnectionStore()
      const getCurrentUser = vi.spyOn(api, 'getCurrentUser')
      await service.connect()

      await service.disconnect()
      await flushPromises()

      expect(getCurrentUser).not.toHaveBeenCalled()
      expect(connection.sessionEnded).toBe(false)
    })
  })
})
