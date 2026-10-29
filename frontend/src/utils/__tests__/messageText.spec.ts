import { describe, expect, it } from 'vitest'

import { MAX_MESSAGE_LENGTH, normalizeMessageText } from '../messageText'

describe('normalizeMessageText', () => {
  it('trims surrounding whitespace', () => {
    expect(normalizeMessageText('  привет \n')).toBe('привет')
  })

  it('rejects an empty or whitespace-only message', () => {
    expect(() => normalizeMessageText('   ')).toThrow(RangeError)
  })

  it('accepts a message exactly at the limit and rejects one character more', () => {
    const atLimit = 'a'.repeat(MAX_MESSAGE_LENGTH)
    expect(normalizeMessageText(atLimit)).toBe(atLimit)
    expect(() => normalizeMessageText(`${atLimit}a`)).toThrow(RangeError)
  })
})
