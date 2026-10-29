<script setup lang="ts">
import { onMounted, ref, useId } from 'vue'

const FIELD_SELECTOR = 'input, textarea, select'

defineProps<{ title: string }>()
const emit = defineEmits<{ close: [] }>()

const dialogRef = ref<HTMLElement | null>(null)
const titleId = useId()

// Focus goes to the first field, so the keyboard user starts typing at once.
onMounted(() => {
  dialogRef.value?.querySelector<HTMLElement>(FIELD_SELECTOR)?.focus()
})

function onKeydown(event: KeyboardEvent): void {
  if (event.key !== 'Escape') return
  event.stopPropagation()
  emit('close')
}
</script>

<template>
  <div class="backdrop">
    <section
      ref="dialogRef"
      class="dialog"
      role="dialog"
      aria-modal="true"
      :aria-labelledby="titleId"
      @keydown="onKeydown"
    >
      <h2 :id="titleId">{{ title }}</h2>
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
  padding: 20px;
  border-radius: 12px;
  background: var(--color-surface);
  box-shadow: 0 12px 32px rgb(0 0 0 / 0.2);
}

h2 {
  margin: 0 0 16px;
  font-size: 1.1rem;
}
</style>
