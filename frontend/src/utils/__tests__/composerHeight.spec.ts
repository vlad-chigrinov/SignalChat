import { describe, expect, it } from 'vitest'

import { COMPOSER_MAX_HEIGHT_PX, fitComposerHeight } from '../composerHeight'

describe('fitComposerHeight', () => {
  it('follows the content height while it is below the maximum', () => {
    expect(fitComposerHeight(42)).toEqual({ heightPx: 42, scrollable: false })
  })

  it('still fits a content exactly as tall as the maximum without a scrollbar', () => {
    expect(fitComposerHeight(COMPOSER_MAX_HEIGHT_PX)).toEqual({
      heightPx: COMPOSER_MAX_HEIGHT_PX,
      scrollable: false,
    })
  })

  it('caps the height and turns scrolling on once the content is taller than the maximum', () => {
    expect(fitComposerHeight(COMPOSER_MAX_HEIGHT_PX + 1)).toEqual({
      heightPx: COMPOSER_MAX_HEIGHT_PX,
      scrollable: true,
    })
  })

  it('takes the maximum from the caller when one is given', () => {
    expect(fitComposerHeight(300, 200)).toEqual({ heightPx: 200, scrollable: true })
  })
})
