<script setup lang="ts">
import { ref } from 'vue'

import { useRoomsStore } from '@/stores/rooms'
import { roomActionFailure } from '@/utils/roomActionError'

const props = defineProps<{ roomId: string }>()
const emit = defineEmits<{ left: [roomId: string] }>()

const rooms = useRoomsStore()
const isLeaving = ref(false)
const leaveError = ref<string | null>(null)

async function leave(): Promise<void> {
  if (isLeaving.value) return
  leaveError.value = null
  isLeaving.value = true
  try {
    await rooms.leaveRoom(props.roomId)
    emit('left', props.roomId)
  } catch (error) {
    const failure = roomActionFailure(error)
    leaveError.value = failure.message
    if (failure.isUnavailable) await rooms.load({ force: true })
  } finally {
    isLeaving.value = false
  }
}
</script>

<template>
  <div class="leave">
    <button type="button" class="button secondary" :disabled="isLeaving" @click="leave">
      Покинуть комнату
    </button>
    <p v-if="leaveError" class="error" role="alert">{{ leaveError }}</p>
  </div>
</template>

<style scoped>
.leave {
  margin-left: auto;
}

.error {
  margin: 4px 0 0;
  color: var(--color-danger);
  font-size: 0.85rem;
}
</style>
