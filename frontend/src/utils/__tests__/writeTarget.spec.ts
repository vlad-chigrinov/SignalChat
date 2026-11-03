import { describe, expect, it } from 'vitest'

import type { DirectConversation, IncomingDirectRequest, UserSummary } from '@/api'

import { resolveWriteTarget } from '../writeTarget'

const ANNA: UserSummary = { id: 'u-anna', displayName: 'Анна', avatarUrl: null }

function direct(status: DirectConversation['status'], peer = ANNA): DirectConversation {
  return { kind: 'direct', id: `dm-${peer.id}`, peer, status, lastMessage: null }
}

const INCOMING: IncomingDirectRequest = {
  id: 'dm-request-anna',
  from: ANNA,
  text: 'Привет',
  createdAt: '2026-11-02T10:00:00.000Z',
}

describe('resolveWriteTarget', () => {
  it('opens the accepted chat that exists', () => {
    expect(resolveWriteTarget(ANNA.id, [direct('accepted')], [])).toEqual({
      kind: 'open',
      conversationId: 'dm-u-anna',
    })
  })

  it('does not send a second request while the first one waits', () => {
    expect(resolveWriteTarget(ANNA.id, [direct('pending')], [])).toEqual({ kind: 'pending' })
  })

  it('points to the request the other user already sent', () => {
    expect(resolveWriteTarget(ANNA.id, [], [INCOMING])).toEqual({ kind: 'incoming' })
  })

  it('starts a request when there is no chat at all', () => {
    expect(resolveWriteTarget(ANNA.id, [], [])).toEqual({ kind: 'request' })
  })

  it('ignores chats with other people', () => {
    const igor: UserSummary = { id: 'u-igor', displayName: 'Игорь', avatarUrl: null }

    expect(resolveWriteTarget(ANNA.id, [direct('accepted', igor)], [])).toEqual({ kind: 'request' })
  })
})
