<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'

import BaseDialog from '@/components/BaseDialog.vue'
import type { Room } from '@/api'
import { useRoomsStore } from '@/stores/rooms'
import { errorMessage } from '@/utils/errorMessage'
import { roomActionFailure } from '@/utils/roomActionError'

/** Pause after the last keystroke before the search goes to the server. */
const SEARCH_DEBOUNCE_MS = 300

const emit = defineEmits<{ close: []; joined: [room: Room] }>()

const rooms = useRoomsStore()
const query = ref('')
const isSearching = ref(false)
const searchError = ref<string | null>(null)
const joiningRoomId = ref<string | null>(null)
const joinError = ref<string | null>(null)
let debounceTimer: ReturnType<typeof setTimeout> | undefined

async function search(): Promise<void> {
  isSearching.value = true
  try {
    await rooms.discover(query.value)
    searchError.value = null
  } catch (error) {
    searchError.value = errorMessage(error)
  } finally {
    isSearching.value = false
  }
}

watch(query, () => {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => void search(), SEARCH_DEBOUNCE_MS)
})

onMounted(() => void search())
onBeforeUnmount(() => clearTimeout(debounceTimer))

async function join(room: Room): Promise<void> {
  joinError.value = null
  joiningRoomId.value = room.id
  try {
    await rooms.joinRoom(room.id)
    emit('joined', room)
  } catch (error) {
    const failure = roomActionFailure(error)
    joinError.value = failure.message
    if (failure.isUnavailable) await search()
  } finally {
    joiningRoomId.value = null
  }
}
</script>

<template>
  <BaseDialog title="Найти комнату" @close="emit('close')">
    <label class="field">
      <span>Название комнаты</span>
      <input v-model="query" class="input" type="search" autocomplete="off" />
    </label>

    <p v-if="joinError" class="error" role="alert">{{ joinError }}</p>
    <p v-if="searchError" class="error" role="alert">{{ searchError }}</p>
    <p v-if="isSearching" class="state">Поиск…</p>
    <p v-else-if="!searchError && rooms.discovered.length === 0" class="state">
      Комнаты не найдены.
    </p>

    <ul v-else class="results">
      <li v-for="room in rooms.discovered" :key="room.id" class="result">
        <div class="result-text">
          <p class="result-name">{{ room.name }}</p>
          <p class="result-description">{{ room.description }}</p>
          <p class="result-count">участников: {{ room.memberCount }}</p>
        </div>
        <button
          type="button"
          class="button"
          :disabled="joiningRoomId !== null"
          :aria-label="`Вступить в комнату ${room.name}`"
          @click="join(room)"
        >
          Вступить
        </button>
      </li>
    </ul>
  </BaseDialog>
</template>

<style scoped>
.field {
  display: grid;
  gap: 6px;
  margin-bottom: 12px;
}

.results {
  margin: 0;
  padding: 0;
  list-style: none;
}

.result {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  padding: 12px 0;
  border-bottom: 1px solid var(--color-border);
}

.result-text {
  min-width: 0;
}

.result-text p {
  margin: 0;
  overflow-wrap: anywhere;
}

.result-name {
  font-weight: 600;
}

.result-description,
.result-count,
.state {
  color: var(--color-muted);
  font-size: 0.9rem;
}

.state {
  margin: 0;
}

.error {
  margin: 0 0 12px;
  color: var(--color-danger);
  font-size: 0.9rem;
}
</style>
