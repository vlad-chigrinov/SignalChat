import { describe, expect, it } from 'vitest'

import type { Message } from '@/api'

import { mergeMessages } from '../mergeMessages'

function message(id: string, sentAt: string): Message {
  const author = { id: 'u1', displayName: 'U', avatarUrl: null }
  return { id, conversationId: 'c1', author, text: id, sentAt, attachments: [] }
}

describe('mergeMessages', () => {
  it('prepends an older page in chronological order', () => {
    const latest = [message('m3', '2026-01-01T10:03:00Z')]
    const older = [message('m1', '2026-01-01T10:01:00Z'), message('m2', '2026-01-01T10:02:00Z')]
    expect(mergeMessages(latest, older).map((m) => m.id)).toEqual(['m1', 'm2', 'm3'])
  })

  it('drops a message delivered twice (send response + real-time)', () => {
    const sent = message('m1', '2026-01-01T10:01:00Z')
    expect(mergeMessages([sent], [{ ...sent }])).toHaveLength(1)
  })
})
