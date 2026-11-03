<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'

import type { Attachment } from '@/api'
import { attachmentDownloadSrc, attachmentSrc } from '@/api/apiConfig'
import { isSideways, neighbourIndex, nextRotation } from '@/utils/photoViewer'

const props = defineProps<{ attachments: Attachment[]; startIndex: number }>()
const emit = defineEmits<{ close: [] }>()

const FOCUSABLE = 'button:not([disabled]), a[href]'

const index = ref(props.startIndex)
/** Clockwise degrees; only what is drawn turns, the file does not. */
const rotation = ref(0)
const dialog = ref<HTMLElement | null>(null)
/** Where the focus was before the viewer opened; it goes back there on close. */
const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null

const current = computed(() => props.attachments[index.value])
const src = computed(() => (current.value ? attachmentSrc(current.value.url) : null))
const downloadSrc = computed(() =>
  current.value ? attachmentDownloadSrc(current.value.url) : null,
)
const previousIndex = computed(() => neighbourIndex(index.value, props.attachments.length, -1))
const nextIndex = computed(() => neighbourIndex(index.value, props.attachments.length, 1))
const title = computed(() => `Фото ${index.value + 1} из ${props.attachments.length}`)

// A new photo starts upright.
watch(index, () => {
  rotation.value = 0
})

function go(target: number | null): void {
  if (target !== null) index.value = target
}

function close(): void {
  emit('close')
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
    go(previousIndex.value)
  } else if (event.key === 'ArrowRight') {
    go(nextIndex.value)
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
      data-testid="photo-viewer"
      @keydown="onKeydown"
      @click.self="close"
    >
      <header class="bar" @click.self="close">
        <span class="counter" role="status">{{ title }}</span>
        <div class="actions">
          <button
            type="button"
            class="action"
            aria-label="Перевернуть"
            @click="rotation = nextRotation(rotation)"
          >
            Перевернуть
          </button>
          <a
            v-if="downloadSrc && current"
            class="action"
            :href="downloadSrc"
            :download="current.fileName"
            aria-label="Скачать"
          >
            Скачать
          </a>
          <button type="button" class="action" aria-label="Закрыть" @click="close">Закрыть</button>
        </div>
      </header>

      <div class="stage" @click.self="close">
        <button
          type="button"
          class="nav previous"
          aria-label="Предыдущее фото"
          :disabled="previousIndex === null"
          @click="go(previousIndex)"
        >
          ‹
        </button>
        <img
          v-if="src && current"
          class="picture"
          :class="{ sideways: isSideways(rotation) }"
          :style="{ transform: `rotate(${rotation}deg)` }"
          :src="src"
          :alt="current.fileName"
          data-testid="photo-viewer-image"
          :data-rotation="rotation"
        />
        <button
          type="button"
          class="nav next"
          aria-label="Следующее фото"
          :disabled="nextIndex === null"
          @click="go(nextIndex)"
        >
          ›
        </button>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.viewer {
  position: fixed;
  inset: 0;
  z-index: 50;
  display: flex;
  flex-direction: column;
  background: rgb(0 0 0 / 90%);
  color: #fff;
}

.bar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.action,
.nav {
  padding: 6px 12px;
  border: 1px solid rgb(255 255 255 / 40%);
  border-radius: 8px;
  background: rgb(255 255 255 / 10%);
  color: inherit;
  cursor: pointer;
  font: inherit;
  text-decoration: none;
}

.action:hover,
.nav:hover:not(:disabled) {
  background: rgb(255 255 255 / 25%);
}

.nav:disabled {
  cursor: default;
  opacity: 0.3;
}

.stage {
  display: flex;
  flex: 1;
  gap: 8px;
  align-items: center;
  justify-content: space-between;
  min-height: 0;
  padding: 0 8px 16px;
}

.nav {
  flex: none;
  font-size: 1.5rem;
  line-height: 1;
}

.picture {
  min-width: 0;
  max-width: calc(100% - 120px);
  max-height: 100%;
  object-fit: contain;
  transition: transform 0.2s ease;
}

/* A quarter turn swaps width and height on screen, so the bounds swap with them. */
.picture.sideways {
  max-width: calc(100vh - 120px);
  max-height: calc(100vw - 120px);
}

@media (prefers-reduced-motion: reduce) {
  .picture {
    transition: none;
  }
}
</style>
