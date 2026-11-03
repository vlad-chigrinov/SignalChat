<script setup lang="ts">
import { computed, ref, useTemplateRef, watch } from 'vue'

import { useAutosizeTextarea } from '@/composables/useAutosizeTextarea'
import { MAX_MESSAGE_LENGTH } from '@/stores/messages'
import { draftAfterFailedSend } from '@/utils/composerDraft'
import { errorMessage } from '@/utils/errorMessage'

import AttachmentPicker from './AttachmentPicker.vue'

const DEFAULT_UNAVAILABLE_REASON = 'Нет соединения. Отправка недоступна.'

const props = withDefaults(
  defineProps<{
    send: (text: string) => Promise<void>
    /** Sends the chosen photos with the draft as their caption. The photos are uploaded here, not when they are picked. */
    sendPhotos?: (caption: string, photos: File[]) => Promise<void>
    /** True while the realtime connection is down or the chat is not accepted yet: sending is blocked. */
    unavailable?: boolean
    /** What the user reads while sending is blocked. */
    unavailableReason?: string
  }>(),
  {
    sendPhotos: undefined,
    unavailable: false,
    unavailableReason: DEFAULT_UNAVAILABLE_REASON,
  },
)
/** Emitted while the user types a non-empty draft. The parent throttles it into a hub call. */
const emit = defineEmits<{ typing: [] }>()

const draft = ref('')
const field = useTemplateRef<HTMLTextAreaElement>('field')
useAutosizeTextarea(field, draft)
const isSending = ref(false)
const error = ref<string | null>(null)
const photos = ref<File[]>([])

// A photo message needs no text: the draft is only its caption.
const hasContent = computed(() => photos.value.length > 0 || draft.value.trim().length > 0)
const canSend = computed(() => !props.unavailable && hasContent.value && !isSending.value)

// Watching the draft (not the input event) also covers the reset after a send, which is empty.
watch(draft, (value) => {
  if (value.trim().length > 0) emit('typing')
})

/**
 * The field is cleared and refocused at once, so the user never sees a sent text hang there
 * while the hub answers; `isSending` keeps a double click or Enter from sending it twice.
 * If the send fails the text comes back (see `draftAfterFailedSend`) and the error is shown.
 */
async function submit(): Promise<void> {
  if (!canSend.value) return
  const text = draft.value
  isSending.value = true
  error.value = null
  draft.value = ''
  field.value?.focus()
  try {
    if (photos.value.length > 0 && props.sendPhotos) {
      await props.sendPhotos(text.trim(), photos.value)
      photos.value = []
    } else {
      await props.send(text)
    }
  } catch (caught) {
    draft.value = draftAfterFailedSend(text, draft.value)
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
    <AttachmentPicker
      v-if="sendPhotos"
      v-model:files="photos"
      :disabled="unavailable || isSending"
    />
    <textarea
      id="message-draft"
      ref="field"
      v-model="draft"
      class="input"
      rows="1"
      :maxlength="MAX_MESSAGE_LENGTH"
      :disabled="unavailable"
      :placeholder="photos.length > 0 ? 'Подпись к фото…' : 'Написать сообщение…'"
      @keydown="onKeydown"
    ></textarea>
    <button type="submit" class="button send" :disabled="!canSend">Отправить</button>
    <p v-if="unavailable" class="status" role="status">{{ unavailableReason }}</p>
    <p v-else-if="error" class="error" role="alert">{{ error }}</p>
  </form>
</template>

<style scoped>
/* One rounded bar: the attach button, the field and Send sit inside it, photo previews above them. */
.composer {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: flex-end;
  margin: 4px 8px 8px;
  padding: 4px;
  border: 1px solid var(--color-border);
  border-radius: 24px;
  background: var(--color-surface);
}

.composer:focus-within {
  border-color: var(--color-accent);
}

.input {
  flex: 1;
  min-width: 0;
  padding: 6px 4px;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  outline: none;
  /* The height follows the text (useAutosizeTextarea), so there is no manual handle. */
  overflow-y: hidden;
  resize: none;
}

/* Fully rounded to match the bar; the global button padding is too roomy inside it. */
.send {
  padding: 6px 14px;
  border-radius: 20px;
}

.error,
.status {
  flex-basis: 100%;
  margin: 0;
  padding: 0 10px 4px;
  font-size: 0.875rem;
}

.error {
  color: var(--color-danger);
}

.status {
  color: var(--color-warning);
}
</style>
