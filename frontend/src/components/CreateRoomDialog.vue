<script setup lang="ts">
import { ref } from 'vue'

import BaseDialog from '@/components/BaseDialog.vue'
import type { Room } from '@/api'
import { useRoomsStore } from '@/stores/rooms'
import { createRoomFailure, roomNameError } from '@/utils/roomForm'

const emit = defineEmits<{ close: []; created: [room: Room] }>()

const rooms = useRoomsStore()
const name = ref('')
const description = ref('')
const isSubmitting = ref(false)
const nameError = ref<string | null>(null)
const formError = ref<string | null>(null)

async function submit(): Promise<void> {
  nameError.value = roomNameError(name.value)
  formError.value = null
  if (nameError.value !== null || isSubmitting.value) return

  isSubmitting.value = true
  try {
    const room = await rooms.createRoom({
      name: name.value.trim(),
      description: description.value.trim(),
    })
    emit('created', room)
  } catch (error) {
    const failure = createRoomFailure(error)
    nameError.value = failure.field
    formError.value = failure.form
  } finally {
    isSubmitting.value = false
  }
}
</script>

<template>
  <BaseDialog title="Новая комната" @close="emit('close')">
    <form novalidate @submit.prevent="submit">
      <label class="field">
        <span>Название</span>
        <input
          v-model="name"
          class="input"
          type="text"
          autocomplete="off"
          :aria-invalid="nameError !== null"
          aria-describedby="room-name-error"
        />
      </label>
      <p v-if="nameError" id="room-name-error" class="error" role="alert">{{ nameError }}</p>

      <label class="field">
        <span>Описание (необязательно)</span>
        <textarea v-model="description" class="input" rows="3" />
      </label>

      <p v-if="formError" class="error" role="alert">{{ formError }}</p>

      <div class="actions">
        <button type="button" class="button secondary" @click="emit('close')">Отмена</button>
        <button type="submit" class="button" :disabled="isSubmitting">Создать</button>
      </div>
    </form>
  </BaseDialog>
</template>

<style scoped>
.field {
  display: grid;
  gap: 6px;
  margin-bottom: 12px;
}

.error {
  margin: 0 0 12px;
  color: var(--color-danger);
  font-size: 0.9rem;
}

.actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}
</style>
