/** The composer grows with its text up to this height, then scrolls inside (about six lines). */
export const COMPOSER_MAX_HEIGHT_PX = 160

export interface ComposerHeight {
  heightPx: number
  /** True when the text is taller than the field, so the field needs its own scrollbar. */
  scrollable: boolean
}

/** Fits the field to its content (scroll height plus borders), never beyond the maximum. */
export function fitComposerHeight(
  contentHeightPx: number,
  maxHeightPx: number = COMPOSER_MAX_HEIGHT_PX,
): ComposerHeight {
  return {
    heightPx: Math.min(contentHeightPx, maxHeightPx),
    scrollable: contentHeightPx > maxHeightPx,
  }
}
