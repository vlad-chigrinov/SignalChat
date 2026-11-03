import { onBeforeUnmount, watch, type Ref } from 'vue'

/**
 * While `isOpen` is true, a press outside `container` or the Escape key calls `dismiss`.
 * The listeners exist only while the panel is open, so a long list of closed messages costs nothing.
 */
export function useDismissOnOutside(
  isOpen: Ref<boolean>,
  container: Ref<HTMLElement | null>,
  dismiss: (reason: 'outside' | 'escape') => void,
): void {
  function onPointerDown(event: PointerEvent): void {
    const target = event.target
    if (target instanceof Node && container.value?.contains(target)) return
    dismiss('outside')
  }

  function onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Escape') dismiss('escape')
  }

  function stopListening(): void {
    document.removeEventListener('pointerdown', onPointerDown, true)
    document.removeEventListener('keydown', onKeyDown)
  }

  watch(
    isOpen,
    (open) => {
      stopListening()
      if (!open) return
      document.addEventListener('pointerdown', onPointerDown, true)
      document.addEventListener('keydown', onKeyDown)
    },
    { flush: 'post' },
  )

  onBeforeUnmount(stopListening)
}
