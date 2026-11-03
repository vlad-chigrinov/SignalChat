import { describe, expect, it } from 'vitest'

import type { Message, UserSummary } from '@/api'

import { buildTimeline, type RoomNotice } from '../timeline'

const ANNA: UserSummary = { id: 'u-anna', displayName: 'Анна', avatarUrl: null }

function message(id: string, sentAt: string): Message {
  return { id, conversationId: 'room-1', author: ANNA, text: id, sentAt }
}

function notice(id: string, at: string): RoomNotice {
  return { id, kind: 'member-joined', user: ANNA, at }
}

function keys(entries: ReturnType<typeof buildTimeline>): string[] {
  return entries.map((entry) => entry.key)
}

describe('buildTimeline', () => {
  it('is only the messages when there are no notices', () => {
    const entries = buildTimeline([message('a', '2026-11-02T10:00:00.000Z')], [])

    expect(keys(entries)).toEqual(['message:a'])
  })

  it('puts a notice between the messages around its time', () => {
    const entries = buildTimeline(
      [message('a', '2026-11-02T10:00:00.000Z'), message('b', '2026-11-02T10:10:00.000Z')],
      [notice('n', '2026-11-02T10:05:00.000Z')],
    )

    expect(keys(entries)).toEqual(['message:a', 'notice:n', 'message:b'])
  })

  it('puts a notice after the last message when it is the newest', () => {
    const entries = buildTimeline(
      [message('a', '2026-11-02T10:00:00.000Z')],
      [notice('n', '2026-11-02T11:00:00.000Z')],
    )

    expect(keys(entries)).toEqual(['message:a', 'notice:n'])
  })

  it('compares moments, not text: +00:00 and Z are the same instant', () => {
    const entries = buildTimeline(
      [
        message('a', '2026-11-02T10:00:00.000+00:00'),
        message('b', '2026-11-02T10:10:00.000+00:00'),
      ],
      [notice('n', '2026-11-02T10:05:00.000Z')],
    )

    expect(keys(entries)).toEqual(['message:a', 'notice:n', 'message:b'])
  })

  it('keeps notices of the same moment in the order they came', () => {
    const entries = buildTimeline(
      [],
      [notice('first', '2026-11-02T10:00:00.000Z'), notice('second', '2026-11-02T10:00:00.000Z')],
    )

    expect(keys(entries)).toEqual(['notice:first', 'notice:second'])
  })

  it('does not change the messages it was given', () => {
    const messages = [message('a', '2026-11-02T10:00:00.000Z')]

    buildTimeline(messages, [notice('n', '2026-11-02T09:00:00.000Z')])

    expect(messages).toHaveLength(1)
  })
})
