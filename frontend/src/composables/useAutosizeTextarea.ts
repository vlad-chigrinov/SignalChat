import { onBeforeUnmount, onMounted, watch, type Ref } from 'vue'

import { fitComposerHeight } from '@/utils/composerHeight'

/**
 * Keeps a textarea as tall as its text (see `fitComposerHeight`). It re-measures after every
 * change of the value (typing, paste, delete, clearing after a send) and when the field's width
 * changes, because a narrower field wraps the same text into more lines.
 */
export function useAutosizeTextarea(field: Ref<HTMLTextAreaElement | null>, value: Ref<string>) {
  let lastWidthPx = 0
  let widthObserver: ResizeObserver | null = null

  function resize(): void {
    const element = field.value
    if (!element) return
    // Measure from the one-row height, without a scrollbar eating into the width.
    element.style.overflowY = 'hidden'
    element.style.height = 'auto'
    const borderHeightPx = element.offsetHeight - element.clientHeight
    const { heightPx, scrollable } = fitComposerHeight(element.scrollHeight + borderHeightPx)
    element.style.height = `${heightPx}px`
    element.style.overflowY = scrollable ? 'auto' : 'hidden'
  }

  // After the DOM update, so the textarea already holds the new value.
  watch(value, resize, { flush: 'post' })

  onMounted(() => {
    resize()
    if (!field.value) return
    widthObserver = new ResizeObserver(([entry]) => {
      if (!entry || entry.contentRect.width === lastWidthPx) return
      lastWidthPx = entry.contentRect.width
      resize()
    })
    widthObserver.observe(field.value)
  })

  onBeforeUnmount(() => widthObserver?.disconnect())
}
