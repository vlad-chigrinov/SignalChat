<script setup lang="ts">
import { computed, ref } from 'vue'

import { MAX_MESSAGE_LENGTH } from '@/utils/messageText'

const props = defineProps<{ peerName: string; isSending: boolean }>()
const emit = defineEmits<{ submit: [text: string]; back: [] }>()

const draft = ref('')
const trimmedLength = computed(() => draft.value.trim().length)
const canSubmit = computed(
  () => !props.isSending && trimmedLength.value > 0 && trimmedLength.value <= MAX_MESSAGE_LENGTH,
)

function submit(): void {
  if (canSubmit.value) emit('submit', draft.value.trim())
}
</script>

<template>
  <form class="request" @submit.prevent="submit">
    <label class="field">
      <span>Первое сообщение для {{ peerName }}</span>
      <textarea
        v-model="draft"
        class="input"
        rows="4"
        :maxlength="MAX_MESSAGE_LENGTH"
        aria-describedby="request-hint request-counter"
      ></textarea>
    </label>
    <p id="request-hint" class="hint">
      Собеседник получит запрос и сможет принять его или отклонить.
    </p>
    <p id="request-counter" class="counter">{{ trimmedLength }} / {{ MAX_MESSAGE_LENGTH }}</p>
    <div class="actions">
      <button type="button" class="button secondary" @click="emit('back')">Назад</button>
      <button type="submit" class="button" :disabled="!canSubmit">Отправить запрос</button>
    </div>
  </form>
</template>

<style scoped>
.field {
  display: grid;
  gap: 6px;
  margin-bottom: 8px;
}

.hint,
.counter {
  margin: 0 0 12px;
  color: var(--color-muted);
  font-size: 0.9rem;
}

.counter {
  margin-top: -6px;
  text-align: right;
}

.actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}
</style>
