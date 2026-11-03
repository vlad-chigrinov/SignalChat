import { describe, expect, it } from 'vitest'

import { PICKER_HEIGHT_PX, pickerPlacement } from '../pickerPlacement'

describe('pickerPlacement', () => {
  it('opens above the message when there is room for the panel', () => {
    expect(pickerPlacement(PICKER_HEIGHT_PX)).toBe('above')
    expect(pickerPlacement(400)).toBe('above')
  })

  it('opens below the message near the top of the list, where the panel would be clipped', () => {
    expect(pickerPlacement(PICKER_HEIGHT_PX - 1)).toBe('below')
    expect(pickerPlacement(0)).toBe('below')
    expect(pickerPlacement(-20)).toBe('below')
  })
})
