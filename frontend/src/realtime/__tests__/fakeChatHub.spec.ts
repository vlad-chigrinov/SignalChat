import { describe, expect, it, vi } from 'vitest'

import type { Message, UserSummary } from '@/api'

import { HubCallError } from '../chatHub'
import { FakeChatHub } from '../fakeChatHub'

const ME: UserSummary = { id: 'me', displayName: 'Я', avatarUrl: null }

function connectedHub(currentUser: UserSummary | null = ME): FakeChatHub {
  return new FakeChatHub({ currentUser: () => currentUser })
}

describe('FakeChatHub', () => {
  it('echoes a sent message to listeners, sender included, and acks with it', async () => {
    const hub = connectedHub()
    await hub.start()
    const received: Message[] = []
    hub.on('MessageReceived', (message) => received.push(message))

    const ack = await hub.sendMessage('room-1', 'привет')

    expect(ack.author).toEqual(ME)
    expect(ack.conversationId).toBe('room-1')
    expect(received).toEqual([ack])
  })

  it('rejects sends when signed out', async () => {
    const hub = connectedHub(null)
    await hub.start()

    await expect(hub.sendMessage('room-1', 'x')).rejects.toMatchObject({
      code: 'unauthenticated',
    })
  })

  it('refuses hub calls while disconnected', async () => {
    const hub = connectedHub()

    await expect(hub.joinRoom('room-1')).rejects.toBeInstanceOf(HubCallError)
  })

  it('tracks joined conversations and forgets them on leave and on stop', async () => {
    const hub = connectedHub()
    await hub.start()
    await hub.joinRoom('room-1')
    await hub.joinRoom('dm-1')
    await hub.leaveRoom('room-1')
    expect(hub.joinedConversations()).toEqual(['dm-1'])

    await hub.stop()
    expect(hub.joinedConversations()).toEqual([])
    expect(hub.currentState()).toBe('disconnected')
  })

  it('delivers typing, presence and member events to subscribers only until unsubscribed', () => {
    const hub = connectedHub()
    const onTyping = vi.fn()
    const unsubscribe = hub.on('UserTyping', onTyping)

    hub.emitTyping({ conversationId: 'room-1', userId: 'peer' })
    unsubscribe()
    hub.emitTyping({ conversationId: 'room-1', userId: 'peer' })

    expect(onTyping).toHaveBeenCalledTimes(1)
    expect(onTyping).toHaveBeenCalledWith({ conversationId: 'room-1', userId: 'peer' })
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

    await hub.typing('room-1')
    await hub.typing('dm-1')

    expect(hub.typingCalls()).toEqual(['room-1', 'dm-1'])
  })
})
