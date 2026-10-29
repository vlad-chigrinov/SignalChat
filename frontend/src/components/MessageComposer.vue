<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { MAX_MESSAGE_LENGTH } from '@/stores/messages'
import { errorMessage } from '@/utils/errorMessage'

const props = withDefaults(
  defineProps<{
    send: (text: string) => Promise<void>
    /** True while the realtime connection is down: the draft is kept, sending is blocked. */
    unavailable?: boolean
  }>(),
  { unavailable: false },
)
/** Emitted while the user types a non-empty draft. The parent throttles it into a hub call. */
const emit = defineEmits<{ typing: [] }>()

const draft = ref('')
const isSending = ref(false)
const error = ref<string | null>(null)

const canSend = computed(
  () => !props.unavailable && draft.value.trim().length > 0 && !isSending.value,
)

// Watching the draft (not the input event) also covers the reset after a send, which is empty.
watch(draft, (value) => {
  if (value.trim().length > 0) emit('typing')
})

async function submit(): Promise<void> {
  if (!canSend.value) return
  isSending.value = true
  error.value = null
  try {
    await props.send(draft.value)
    draft.value = ''
  } catch (caught) {
    error.value = errorMessage(caught)
  } finally {
    isSending.value = false
  }
}

/** Enter sends, Shift+Enter inserts a line break; IME composition is left alone. */
function onKeydown(event: KeyboardEvent): void {
  if (event.key !== 'Enter' || event.shiftKey || event.isComposing) return
  event.preventDefault()
  void submit()
}
</script>

<template>
  <form class="composer" @submit.prevent="submit">
    <label class="sr-only" for="message-draft">Сообщение</label>
    <textarea
      id="message-draft"
      v-model="draft"
      class="input"
      rows="1"
      :maxlength="MAX_MESSAGE_LENGTH"
      :disabled="unavailable"
      placeholder="Написать сообщение…"
      @keydown="onKeydown"
    ></textarea>
    <button type="submit" class="button" :disabled="!canSend">Отправить</button>
    <p v-if="unavailable" class="status" role="status">Нет соединения. Отправка недоступна.</p>
    <p v-else-if="error" class="error" role="alert">{{ error }}</p>
  </form>
</template>

<style scoped>
.composer {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  padding: 12px 16px;
  border-top: 1px solid var(--color-border);
  background: var(--color-surface);
}

.input {
  flex: 1;
  min-width: 0;
  max-height: 160px;
  padding: 8px 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg);
  color: inherit;
  font: inherit;
  resize: vertical;
}

.error,
.status {
  flex-basis: 100%;
  margin: 0;
  font-size: 0.875rem;
}

.error {
  color: var(--color-danger);
}

.status {
  color: var(--color-warning);
}
</style>
