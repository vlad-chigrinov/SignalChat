<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'

import BaseDialog from '@/components/BaseDialog.vue'
import DirectRequestForm from '@/components/DirectRequestForm.vue'
import UserAvatar from '@/components/UserAvatar.vue'
import type { DirectConversation, UserSummary } from '@/api'
import { useDirectRequestSend } from '@/composables/useDirectRequestSend'
import { useUserSearchStore } from '@/stores/userSearch'
import { errorMessage } from '@/utils/errorMessage'
import { userQueryHint } from '@/utils/userSearch'

/** Pause after the last keystroke before the search goes to the server. */
const SEARCH_DEBOUNCE_MS = 300

const emit = defineEmits<{ close: []; opened: [direct: DirectConversation] }>()

const userSearch = useUserSearchStore()
const query = ref('')
const hint = ref<string | null>(null)
const searchError = ref<string | null>(null)
const { isSending, sendError, send, clearError } = useDirectRequestSend()
/** The user picked from the results; the request text is written for them next. */
const chosen = ref<UserSummary | null>(null)
let debounceTimer: ReturnType<typeof setTimeout> | undefined

async function search(): Promise<void> {
  searchError.value = null
  try {
    await userSearch.search(query.value)
  } catch (error) {
    searchError.value = errorMessage(error)
  }
}

watch(query, () => {
  clearTimeout(debounceTimer)
  hint.value = userQueryHint(query.value)
  if (hint.value !== null) {
    userSearch.clear()
    return
  }
  debounceTimer = setTimeout(() => void search(), SEARCH_DEBOUNCE_MS)
})

onBeforeUnmount(() => {
  clearTimeout(debounceTimer)
  userSearch.clear()
})

function choose(user: UserSummary): void {
  clearError()
  chosen.value = user
}

/** An accepted chat opens as it is; a new request opens as pending (the view shows it so). */
async function sendRequest(text: string): Promise<void> {
  if (chosen.value === null) return
  const direct = await send(chosen.value.id, text)
  if (direct) emit('opened', direct)
}
</script>

<template>
  <BaseDialog title="Новое сообщение" @close="emit('close')">
    <template v-if="chosen">
      <p v-if="sendError" class="error" role="alert">{{ sendError }}</p>
      <DirectRequestForm
        :peer-name="chosen.displayName"
        :is-sending="isSending"
        @submit="sendRequest"
        @back="chosen = null"
      />
    </template>

    <template v-else>
      <label class="field">
        <span>Имя собеседника</span>
        <input
          v-model="query"
          class="input"
          type="search"
          autocomplete="off"
          :aria-describedby="hint ? 'user-search-hint' : undefined"
        />
      </label>
      <p v-if="hint" id="user-search-hint" class="hint">{{ hint }}</p>
      <p v-if="searchError" class="error" role="alert">{{ searchError }}</p>

      <ul v-if="userSearch.results.length > 0" class="results">
        <li v-for="user in userSearch.results" :key="user.id">
          <button
            type="button"
            class="result"
            :aria-label="`Написать: ${user.displayName}`"
            @click="choose(user)"
          >
            <UserAvatar :user="user" :size="32" />
            <span>{{ user.displayName }}</span>
          </button>
        </li>
      </ul>
      <p v-else-if="hint === null && query.trim() !== ''" class="hint">Никого не нашли.</p>
    </template>
  </BaseDialog>
</template>

<style scoped>
.field {
  display: grid;
  gap: 6px;
  margin-bottom: 12px;
}

.hint {
  margin: 0 0 12px;
  color: var(--color-muted);
  font-size: 0.9rem;
}

.error {
  margin: 0 0 12px;
  color: var(--color-danger);
  font-size: 0.9rem;
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
  width: 100%;
  padding: 8px;
  border: 0;
  border-radius: 8px;
  background: transparent;
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.result:hover {
  background: var(--color-accent-soft);
}
</style>
