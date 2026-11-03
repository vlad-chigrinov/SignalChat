import { describe, expect, it } from 'vitest'

import { isSideways, neighbourIndex, nextRotation } from '../photoViewer'

describe('nextRotation', () => {
  it('turns a quarter clockwise each time and comes back to the start after four', () => {
    expect(nextRotation(0)).toBe(90)
    expect(nextRotation(90)).toBe(180)
    expect(nextRotation(180)).toBe(270)
    expect(nextRotation(270)).toBe(0)
  })
})

describe('isSideways', () => {
  it('is true for a quarter turn and a three-quarter turn only', () => {
    expect([0, 90, 180, 270].map(isSideways)).toEqual([false, true, false, true])
  })
})

describe('neighbourIndex', () => {
  it('moves to the next and the previous photo', () => {
    expect(neighbourIndex(1, 3, 1)).toBe(2)
    expect(neighbourIndex(1, 3, -1)).toBe(0)
  })

  it('stops at both ends instead of wrapping around', () => {
    expect(neighbourIndex(2, 3, 1)).toBeNull()
    expect(neighbourIndex(0, 3, -1)).toBeNull()
  })

  it('has no neighbour for a single photo', () => {
    expect(neighbourIndex(0, 1, 1)).toBeNull()
    expect(neighbourIndex(0, 1, -1)).toBeNull()
  })
})
