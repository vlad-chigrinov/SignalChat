<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'

import type { Attachment } from '@/api'
import { attachmentSrc } from '@/api/apiConfig'
import { fetchAttachmentFile } from '@/api/attachmentFile'
import { isSideways, neighbourIndex, nextRotation, swipeStep } from '@/utils/photoViewer'
import { saveBlob } from '@/utils/saveBlob'

const props = defineProps<{ attachments: Attachment[]; startIndex: number }>()
const emit = defineEmits<{ close: [] }>()

const FOCUSABLE = 'button:not([disabled])'
const DOWNLOAD_FAILED_TEXT = 'Не удалось скачать фото. Попробуйте ещё раз.'

const index = ref(props.startIndex)
/** Clockwise degrees; only what is drawn turns, the file does not. */
const rotation = ref(0)
const isDownloading = ref(false)
const downloadError = ref<string | null>(null)
const dialog = ref<HTMLElement | null>(null)
/** Where the focus was before the viewer opened; it goes back there on close. */
const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null

const current = computed(() => props.attachments[index.value])
const src = computed(() => (current.value ? attachmentSrc(current.value.url) : null))
const title = computed(() => `Фото ${index.value + 1} из ${props.attachments.length}`)

// A new photo starts upright.
watch(index, () => {
  rotation.value = 0
  downloadError.value = null
})

function step(direction: 1 | -1): void {
  const target = neighbourIndex(index.value, props.attachments.length, direction)
  if (target !== null) index.value = target
}

/** Saves the original under its file name. The bytes are fetched here, so the page is never left. */
async function download(): Promise<void> {
  const photo = current.value
  if (!photo || isDownloading.value) return
  isDownloading.value = true
  downloadError.value = null
  try {
    saveBlob(await fetchAttachmentFile(photo.url), photo.fileName)
  } catch {
    downloadError.value = DOWNLOAD_FAILED_TEXT
  } finally {
    isDownloading.value = false
  }
}

function close(): void {
  emit('close')
}

// Swiping: where the finger (or the mouse) went down, and whether the last press turned into a swipe.
let pressX = 0
let pressY = 0
let isPressed = false
let didSwipe = false

function onPointerDown(event: PointerEvent): void {
  isPressed = true
  didSwipe = false
  pressX = event.clientX
  pressY = event.clientY
}

function onPointerUp(event: PointerEvent): void {
  if (!isPressed) return
  isPressed = false
  const direction = swipeStep(event.clientX - pressX, event.clientY - pressY)
  if (direction === null) return
  didSwipe = true
  step(direction)
}

/** A click outside the photo closes the viewer, but not the click that ends a swipe. */
function onStageClick(): void {
  if (didSwipe) {
    didSwipe = false
    return
  }
  close()
}

function focusables(): HTMLElement[] {
  return Array.from(dialog.value?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? [])
}

/** Escape closes, the arrows page, Tab stays inside the viewer. */
function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    close()
  } else if (event.key === 'ArrowLeft') {
    step(-1)
  } else if (event.key === 'ArrowRight') {
    step(1)
  } else if (event.key === 'Tab') {
    const items = focusables()
    const first = items[0]
    const last = items.at(-1)
    if (!first || !last) return
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault()
      last.focus()
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault()
      first.focus()
    }
  }
}

onMounted(() => {
  document.body.style.overflow = 'hidden'
  void nextTick(() => focusables()[0]?.focus())
})

onBeforeUnmount(() => {
  document.body.style.overflow = ''
  opener?.focus()
})
</script>

<template>
  <Teleport to="body">
    <div
      ref="dialog"
      class="viewer"
      role="dialog"
      aria-modal="true"
      aria-label="Просмотр фото"
      tabindex="-1"
      data-testid="photo-viewer"
      @keydown="onKeydown"
    >
      <div
        class="stage"
        data-testid="photo-viewer-stage"
        @pointerdown="onPointerDown"
        @pointerup="onPointerUp"
        @pointercancel="isPressed = false"
        @click.self="onStageClick"
      >
        <img
          v-if="src && current"
          class="picture"
          :class="{ sideways: isSideways(rotation) }"
          :style="{ transform: `rotate(${rotation}deg)` }"
          :src="src"
          :alt="current.fileName"
          draggable="false"
          data-testid="photo-viewer-image"
          :data-rotation="rotation"
        />
      </div>

      <p v-if="downloadError" class="download-error" role="alert">{{ downloadError }}</p>

      <footer class="bar">
        <span class="counter" role="status">{{ title }}</span>
        <div class="actions">
          <button
            type="button"
            class="action icon-action"
            aria-label="Перевернуть"
            title="Перевернуть"
            @click="rotation = nextRotation(rotation)"
          >
            <svg
              class="icon"
              viewBox="0 0 24 24"
              width="20"
              height="20"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
              focusable="false"
            >
              <path d="M21 12a9 9 0 1 1-3-6.7" />
              <path d="M21 4v5h-5" />
            </svg>
          </button>
          <button
            type="button"
            class="action"
            aria-label="Скачать"
            :disabled="isDownloading"
            @click="download"
          >
            Скачать
          </button>
          <button type="button" class="action" aria-label="Закрыть" @click="close">Закрыть</button>
        </div>
      </footer>
    </div>
  </Teleport>
</template>

<style scoped>
.viewer {
  position: fixed;
  outline: none;
  inset: 0;
  z-index: 50;
  display: flex;
  flex-direction: column;
  background: rgb(0 0 0 / 90%);
  color: #fff;
}

/* The photo is at most 80% of the screen each way; a horizontal swipe on this area pages through the photos. */
.stage {
  display: flex;
  flex: 1;
  align-items: center;
  justify-content: center;
  min-height: 0;
  touch-action: pan-y pinch-zoom;
  user-select: none;
}

.picture {
  max-width: 80vw;
  max-height: 80vh;
  object-fit: contain;
  transition: transform 0.2s ease;
}

/* A quarter turn swaps width and height on screen, so the bounds swap with them. */
.picture.sideways {
  max-width: 80vh;
  max-height: 80vw;
}

.bar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px calc(12px + env(safe-area-inset-bottom));
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.action {
  padding: 6px 12px;
  border: 1px solid rgb(255 255 255 / 40%);
  border-radius: 8px;
  background: rgb(255 255 255 / 10%);
  color: inherit;
  cursor: pointer;
  font: inherit;
}

.icon-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 6px 10px;
}

.icon {
  display: block;
}

.action:disabled {
  cursor: default;
  opacity: 0.5;
}

.action:hover:not(:disabled) {
  background: rgb(255 255 255 / 25%);
}

.download-error {
  margin: 0;
  padding: 0 16px 4px;
  color: #ffb4a8;
  font-size: 0.875rem;
}

@media (prefers-reduced-motion: reduce) {
  .picture {
    transition: none;
  }
}
</style>
