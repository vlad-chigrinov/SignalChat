<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import type { Room } from '@/api'
import { ROUTE_NAMES } from '@/router/routes'
import { useRoomsStore } from '@/stores/rooms'
import { errorMessage } from '@/utils/errorMessage'
import { roomActionFailure } from '@/utils/roomActionError'

/** Pause after the last keystroke before the search goes to the server. */
const SEARCH_DEBOUNCE_MS = 300

const rooms = useRoomsStore()
const router = useRouter()
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
    await router.push({ name: ROUTE_NAMES.chat, params: { id: room.id } })
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
  <main class="discover">
    <header class="discover-header">
      <RouterLink class="back" :to="{ name: ROUTE_NAMES.chatList }" aria-label="К списку чатов">
        ←
      </RouterLink>
      <h1>Найти комнату</h1>
    </header>

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
  </main>
</template>

<style scoped>
.discover {
  width: min(100%, 720px);
  margin: 0 auto;
  padding: 16px;
}

.discover-header {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-bottom: 16px;
}

h1 {
  margin: 0;
  font-size: 1.2rem;
}

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

.result-text p {
  margin: 0;
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

.error {
  color: var(--color-danger);
}
</style>
