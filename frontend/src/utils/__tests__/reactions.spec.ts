import { describe, expect, it } from 'vitest'

import type { Reaction } from '@/api'

import { ownEmojiOf, reactionCountLabel, withCounts, withOwnEmoji } from '../reactions'

const reaction = (emoji: string, count: number, reactedByMe = false): Reaction => ({
  emoji,
  count,
  reactedByMe,
})

describe('reactionCountLabel', () => {
  it('shows no number for a single reaction, and the count from two on', () => {
    expect(reactionCountLabel(1)).toBe('')
    expect(reactionCountLabel(2)).toBe('2')
    expect(reactionCountLabel(15)).toBe('15')
  })
})

describe('ownEmojiOf', () => {
  it('finds the marked emoji, or null', () => {
    expect(ownEmojiOf([reaction('👍', 2), reaction('❤️', 1, true)])).toBe('❤️')
    expect(ownEmojiOf([reaction('👍', 2)])).toBeNull()
    expect(ownEmojiOf([])).toBeNull()
  })
})

describe('withCounts', () => {
  it('takes the new counts and keeps the own emoji marked', () => {
    const current = [reaction('👍', 1, true)]

    const next = withCounts(current, [
      { emoji: '👍', count: 3 },
      { emoji: '😂', count: 1 },
    ])

    expect(next).toEqual([reaction('👍', 3, true), reaction('😂', 1)])
  })

  it('drops the mark when the own emoji is no longer counted', () => {
    const next = withCounts([reaction('👍', 1, true)], [{ emoji: '❤️', count: 1 }])

    expect(next).toEqual([reaction('❤️', 1)])
  })

  it('lists the emoji in catalog order, whatever order the event had', () => {
    const next = withCounts(
      [],
      [
        { emoji: '🎉', count: 1 },
        { emoji: '👍', count: 1 },
      ],
    )

    expect(next.map((item) => item.emoji)).toEqual(['👍', '🎉'])
  })

  it('leaves out an emoji with no one behind it', () => {
    expect(withCounts([], [{ emoji: '👍', count: 0 }])).toEqual([])
  })
})

describe('withOwnEmoji', () => {
  it('marks only the given emoji, and nothing for null', () => {
    const current = [reaction('👍', 2, true), reaction('❤️', 1)]

    expect(withOwnEmoji(current, '❤️')).toEqual([reaction('👍', 2), reaction('❤️', 1, true)])
    expect(withOwnEmoji(current, null)).toEqual([reaction('👍', 2), reaction('❤️', 1)])
  })

  it('does not change the list it was given', () => {
    const current = [reaction('👍', 2, true)]

    withOwnEmoji(current, null)

    expect(current[0]?.reactedByMe).toBe(true)
  })
})
