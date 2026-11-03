import { describe, expect, it } from 'vitest'

import { REACTION_EMOJIS, isReactionEmoji, reactionOrder } from '../reactionCatalog'

describe('reaction catalog', () => {
  // Mirrors ReactionCatalog.Emojis in the backend (MessageReactionTests pins it there too).
  it('is the agreed small list', () => {
    expect([...REACTION_EMOJIS]).toEqual(['👍', '❤️', '😂', '😮', '😢', '🎉'])
  })

  it('accepts only exact members', () => {
    expect(isReactionEmoji('👍')).toBe(true)
    expect(isReactionEmoji('👍👍')).toBe(false)
    expect(isReactionEmoji(' 👍')).toBe(false)
    expect(isReactionEmoji('<b>')).toBe(false)
    expect(isReactionEmoji('')).toBe(false)
  })

  it('orders by the display order and puts unknown emoji last', () => {
    expect(reactionOrder('👍')).toBeLessThan(reactionOrder('🎉'))
    expect(reactionOrder('💩')).toBe(REACTION_EMOJIS.length)
  })
})
