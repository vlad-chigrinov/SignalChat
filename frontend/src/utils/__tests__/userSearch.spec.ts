import { describe, expect, it } from 'vitest'

import { userQueryHint } from '../userSearch'

describe('userQueryHint', () => {
  it('asks for at least two characters, after trim', () => {
    expect(userQueryHint('')).not.toBeNull()
    expect(userQueryHint('  а  ')).not.toBeNull()
  })

  it('allows a search from two characters', () => {
    expect(userQueryHint('ан')).toBeNull()
    expect(userQueryHint('  Анна ')).toBeNull()
  })
})
