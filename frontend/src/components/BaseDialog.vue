<script setup lang="ts">
import { onMounted, ref, useId } from 'vue'

const FIELD_SELECTOR = 'input, textarea, select'

defineProps<{ title: string }>()
const emit = defineEmits<{ close: [] }>()

const dialogRef = ref<HTMLElement | null>(null)
const titleId = useId()
/** The press began on the backdrop itself: a drag that started in a field must not close the dialog. */
let isPressOnBackdrop = false

// Focus goes to the first field, so the keyboard user starts typing at once.
onMounted(() => {
  dialogRef.value?.querySelector<HTMLElement>(FIELD_SELECTOR)?.focus()
})

function onKeydown(event: KeyboardEvent): void {
  if (event.key !== 'Escape') return
  event.stopPropagation()
  emit('close')
}

function onBackdropMouseDown(): void {
  isPressOnBackdrop = true
}

function onBackdropClick(): void {
  const closes = isPressOnBackdrop
  isPressOnBackdrop = false
  if (closes) emit('close')
}
</script>

<template>
  <div class="backdrop" @mousedown.self="onBackdropMouseDown" @click.self="onBackdropClick">
    <section
      ref="dialogRef"
      class="dialog"
      role="dialog"
      aria-modal="true"
      :aria-labelledby="titleId"
      @keydown="onKeydown"
    >
      <header class="dialog-header">
        <h2 :id="titleId">{{ title }}</h2>
        <button type="button" class="close" aria-label="Закрыть" @click="emit('close')">×</button>
      </header>
      <slot />
    </section>
  </div>
</template>

<style scoped>
.backdrop {
  position: fixed;
  inset: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
  background: rgb(0 0 0 / 0.4);
}

.dialog {
  width: min(100%, 420px);
  max-height: 100%;
  padding: 20px;
  overflow-y: auto;
  border-radius: 12px;
  background: var(--color-surface);
  box-shadow: 0 12px 32px rgb(0 0 0 / 0.2);
}

.dialog-header {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 16px;
}

h2 {
  margin: 0;
  font-size: 1.1rem;
}

.close {
  flex: none;
  width: 32px;
  height: 32px;
  margin: -4px -6px 0 0;
  padding: 0;
  border: 0;
  border-radius: 8px;
  background: transparent;
  color: var(--color-muted);
  font: inherit;
  font-size: 1.5rem;
  line-height: 1;
  cursor: pointer;
}

.close:hover {
  background: var(--color-accent-soft);
  color: inherit;
}
</style>
