import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { Message, UserSummary } from '@/api'
import { mockCurrentUser } from '@/api/mock/mockData'
import { historyOf, resetSharedMockStore, sharedMockStore } from '@/api/mock/mockHistories'

import { HubCallError } from '../chatHub'
import { FakeChatHub } from '../fakeChatHub'

/** The signed-in demo user: a member of the seeded rooms and DMs. */
const ME: UserSummary = {
  id: mockCurrentUser.id,
  displayName: mockCurrentUser.displayName,
  avatarUrl: mockCurrentUser.avatarUrl,
}
/** Seeded room the signed-in user is NOT a member of (discovery only). */
const NON_MEMBER_ROOM = 'room-chess'

function connectedHub(currentUser: UserSummary | null = ME): FakeChatHub {
  return new FakeChatHub({ currentUser: () => currentUser })
}

describe('FakeChatHub', () => {
  beforeEach(() => {
    resetSharedMockStore()
  })

  it('echoes a sent message to listeners, sender included, and acks with it', async () => {
    const hub = connectedHub()
    await hub.start()
    const received: Message[] = []
    hub.on('MessageReceived', (message) => received.push(message))

    const ack = await hub.sendMessage('room-general', 'привет')

    expect(ack.author).toEqual(ME)
    expect(ack.conversationId).toBe('room-general')
    expect(received).toEqual([ack])
  })

  it('rejects sends when signed out', async () => {
    const hub = connectedHub(null)
    await hub.start()

    await expect(hub.sendMessage('room-general', 'x')).rejects.toMatchObject({
      code: 'unauthenticated',
    })
  })

  it('refuses hub calls while disconnected', async () => {
    const hub = connectedHub()

    await expect(hub.joinRoom('room-general')).rejects.toBeInstanceOf(HubCallError)
  })

  it('answers not_found for an unknown conversation, on join and on send', async () => {
    const hub = connectedHub()
    await hub.start()

    await expect(hub.joinRoom('room-missing')).rejects.toMatchObject({ code: 'not_found' })
    await expect(hub.sendMessage('room-missing', 'x')).rejects.toMatchObject({
      code: 'not_found',
    })
  })

  it('answers not_found for a conversation the caller is not a member of', async () => {
    const hub = connectedHub()
    await hub.start()

    await expect(hub.joinRoom(NON_MEMBER_ROOM)).rejects.toMatchObject({ code: 'not_found' })
    await expect(hub.sendMessage(NON_MEMBER_ROOM, 'x')).rejects.toMatchObject({
      code: 'not_found',
    })
  })

  it('stores a sent message in the shared mock history so a later REST read sees it', async () => {
    const hub = connectedHub()
    await hub.start()

    const sent = await hub.sendMessage('dm-anna', 'из хаба')

    expect(historyOf(sharedMockStore(), 'dm-anna').at(-1)).toEqual(sent)
  })

  it('reports a server-side close to onClosed, but not our own stop', async () => {
    const hub = connectedHub()
    await hub.start()
    const onClosed = vi.fn()
    hub.onClosed(onClosed)

    await hub.stop()
    expect(onClosed).not.toHaveBeenCalled()

    await hub.start()
    hub.simulateServerClose()
    expect(onClosed).toHaveBeenCalledTimes(1)
    expect(hub.currentState()).toBe('disconnected')
  })

  it('tracks joined conversations and forgets them on leave and on stop', async () => {
    const hub = connectedHub()
    await hub.start()
    await hub.joinRoom('room-general')
    await hub.joinRoom('dm-anna')
    await hub.leaveRoom('room-general')
    expect(hub.joinedConversations()).toEqual(['dm-anna'])

    await hub.stop()
    expect(hub.joinedConversations()).toEqual([])
    expect(hub.currentState()).toBe('disconnected')
  })

  it('delivers typing, presence and member events to subscribers only until unsubscribed', () => {
    const hub = connectedHub()
    const onTyping = vi.fn()
    const unsubscribe = hub.on('UserTyping', onTyping)

    hub.emitTyping({ conversationId: 'room-general', userId: 'peer' })
    unsubscribe()
    hub.emitTyping({ conversationId: 'room-general', userId: 'peer' })

    expect(onTyping).toHaveBeenCalledTimes(1)
    expect(onTyping).toHaveBeenCalledWith({ conversationId: 'room-general', userId: 'peer' })
  })

  it('notifies reconnect listeners after simulateReconnect', async () => {
    const hub = connectedHub()
    await hub.start()
    const onReconnected = vi.fn()
    const states: string[] = []
    hub.onReconnected(onReconnected)
    hub.onConnectionState((state) => states.push(state))

    hub.simulateReconnect()

    expect(onReconnected).toHaveBeenCalledTimes(1)
    expect(states).toEqual(['reconnecting', 'connected'])
  })

  it('records typing calls in order', async () => {
    const hub = connectedHub()
    await hub.start()

    await hub.typing('room-general')
    await hub.typing('dm-anna')

    expect(hub.typingCalls()).toEqual(['room-general', 'dm-anna'])
  })
})
