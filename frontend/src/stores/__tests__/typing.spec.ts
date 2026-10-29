import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import type { CurrentUser } from '@/api'
import { TYPING_INDICATOR_TIMEOUT_MS } from '@/realtime/hubContract'

import { useAuthStore } from '../auth'
import { useTypingStore } from '../typing'

const ME: CurrentUser = {
  id: 'me',
  displayName: 'Я',
  avatarUrl: null,
  email: 'me@example.test',
}

describe('typing store', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    setActivePinia(createPinia())
    useAuthStore().user = ME
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('shows a typing user and hides them after the indicator timeout', () => {
    const typing = useTypingStore()

    typing.applyUserTyping({ conversationId: 'room-1', userId: 'peer' })
    expect(typing.typingUserIds('room-1')).toEqual(['peer'])

    vi.advanceTimersByTime(TYPING_INDICATOR_TIMEOUT_MS - 1)
    expect(typing.typingUserIds('room-1')).toEqual(['peer'])

    vi.advanceTimersByTime(1)
    expect(typing.typingUserIds('room-1')).toEqual([])
  })

  it('renews the timer when the same user types again', () => {
    const typing = useTypingStore()

    typing.applyUserTyping({ conversationId: 'room-1', userId: 'peer' })
    vi.advanceTimersByTime(TYPING_INDICATOR_TIMEOUT_MS - 1000)
    typing.applyUserTyping({ conversationId: 'room-1', userId: 'peer' })
    vi.advanceTimersByTime(TYPING_INDICATOR_TIMEOUT_MS - 1000)

    expect(typing.typingUserIds('room-1')).toEqual(['peer'])
    expect(typing.typingUserIds('room-1')).toHaveLength(1)
  })

  it('keeps users separate per conversation', () => {
    const typing = useTypingStore()

    typing.applyUserTyping({ conversationId: 'room-1', userId: 'a' })
    typing.applyUserTyping({ conversationId: 'dm-1', userId: 'b' })

    expect(typing.typingUserIds('room-1')).toEqual(['a'])
    expect(typing.typingUserIds('dm-1')).toEqual(['b'])
  })

  it('never shows the current user as typing', () => {
    const typing = useTypingStore()

    typing.applyUserTyping({ conversationId: 'room-1', userId: ME.id })

    expect(typing.typingUserIds('room-1')).toEqual([])
  })

  it('clears a user when a message from them arrives', () => {
    const typing = useTypingStore()
    typing.applyUserTyping({ conversationId: 'room-1', userId: 'a' })
    typing.applyUserTyping({ conversationId: 'room-1', userId: 'b' })

    typing.clearUser('room-1', 'a')

    expect(typing.typingUserIds('room-1')).toEqual(['b'])
    vi.advanceTimersByTime(TYPING_INDICATOR_TIMEOUT_MS)
    expect(typing.typingUserIds('room-1')).toEqual([])
  })

  it('clears a whole conversation and resets everything', () => {
    const typing = useTypingStore()
    typing.applyUserTyping({ conversationId: 'room-1', userId: 'a' })
    typing.applyUserTyping({ conversationId: 'dm-1', userId: 'b' })

    typing.clearConversation('room-1')
    expect(typing.typingUserIds('room-1')).toEqual([])
    expect(typing.typingUserIds('dm-1')).toEqual(['b'])

    typing.reset()
    expect(typing.typingUserIds('dm-1')).toEqual([])
  })
})
