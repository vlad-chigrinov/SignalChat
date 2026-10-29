<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'

import MessageComposer from '@/components/MessageComposer.vue'
import MessageList from '@/components/MessageList.vue'
import PresenceDot from '@/components/PresenceDot.vue'
import { ROUTE_NAMES } from '@/router/routes'
import { useAuthStore } from '@/stores/auth'
import { useMessagesStore } from '@/stores/messages'
import { usePresenceStore } from '@/stores/presence'
import { useRoomsStore } from '@/stores/rooms'

const props = defineProps<{ id: string }>()

const auth = useAuthStore()
const rooms = useRoomsStore()
const messages = useMessagesStore()
const presence = usePresenceStore()

const conversation = computed(() => rooms.findConversation(props.id))
const history = computed(() => messages.historyOf(props.id))
const isUnknown = computed(() => rooms.status === 'ready' && !conversation.value)

const title = computed(() => {
  const current = conversation.value
  if (!current) return 'Чат'
  return current.kind === 'room' ? `# ${current.name}` : current.peer.displayName
})

const subtitle = computed(() => {
  const current = conversation.value
  if (!current) return ''
  if (current.kind === 'room') return `${current.description} · участников: ${current.memberCount}`
  return presence.isOnline(current.peer.id) ? 'в сети' : 'не в сети'
})

onMounted(() => void rooms.load())
watch(
  () => props.id,
  (id) => void messages.loadLatest(id),
  { immediate: true },
)

const send = (text: string) => messages.send(props.id, text)
</script>

<template>
  <main class="chat">
    <header class="chat-header">
      <RouterLink class="back" :to="{ name: ROUTE_NAMES.chatList }" aria-label="К списку чатов">
        ←
      </RouterLink>
      <div class="heading">
        <h1>{{ title }}</h1>
        <p class="subtitle">
          <PresenceDot
            v-if="conversation?.kind === 'direct'"
            :online="presence.isOnline(conversation.peer.id)"
          />
          {{ subtitle }}
        </p>
      </div>
    </header>

    <p v-if="isUnknown" class="state" role="alert">Чат не найден или у вас нет к нему доступа.</p>

    <template v-else>
      <p v-if="history.error" class="state error" role="alert">
        {{ history.error }}
        <button
          type="button"
          class="link-button"
          @click="history.hasLoadedLatest ? messages.loadOlder(id) : messages.loadLatest(id)"
        >
          Повторить
        </button>
      </p>
      <MessageList
        :key="id"
        :messages="history.items"
        :has-more="history.nextCursor !== null"
        :is-loading="history.isLoading"
        :current-user-id="auth.user?.id ?? null"
        @load-older="messages.loadOlder(id)"
      />
      <MessageComposer :send="send" />
    </template>
  </main>
</template>

<style scoped>
.chat {
  display: flex;
  flex-direction: column;
  width: min(100%, 960px);
  margin: 0 auto;
}

.chat-header {
  display: flex;
  gap: 12px;
  align-items: center;
  padding: 8px 16px;
  border-bottom: 1px solid var(--color-border);
}

.back {
  color: var(--color-muted);
  font-size: 1.25rem;
  text-decoration: none;
}

.heading {
  min-width: 0;
}

h1 {
  margin: 0;
  overflow: hidden;
  font-size: 1.1rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.subtitle {
  display: flex;
  gap: 6px;
  align-items: center;
  margin: 0;
  color: var(--color-muted);
  font-size: 0.8rem;
}

.state {
  padding: 12px 16px;
  color: var(--color-muted);
}

.state.error {
  margin: 0;
  color: var(--color-danger);
}
</style>
