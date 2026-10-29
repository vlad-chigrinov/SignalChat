<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'

import LeaveRoomButton from '@/components/LeaveRoomButton.vue'
import MessageComposer from '@/components/MessageComposer.vue'
import MessageList from '@/components/MessageList.vue'
import PresenceDot from '@/components/PresenceDot.vue'
import TypingIndicator from '@/components/TypingIndicator.vue'
import { getRealtimeService } from '@/realtime'
import { ROUTE_NAMES } from '@/router/routes'
import { useAuthStore } from '@/stores/auth'
import { useConnectionStore } from '@/stores/connection'
import { useMessagesStore } from '@/stores/messages'
import { usePresenceStore } from '@/stores/presence'
import { useRoomsStore } from '@/stores/rooms'
import { useTypingStore } from '@/stores/typing'
import type { UserId } from '@/api'

const NAME_OF_UNKNOWN_AUTHOR = 'Кто-то'

const props = defineProps<{ id: string }>()

const auth = useAuthStore()
const rooms = useRoomsStore()
const messages = useMessagesStore()
const presence = usePresenceStore()
const typing = useTypingStore()
const connection = useConnectionStore()
const realtime = getRealtimeService()
const router = useRouter()

/** Sending is hub-only (C1): without a live connection the composer is blocked. */
const isSendingUnavailable = computed(() => connection.state !== 'connected')

const conversation = computed(() => rooms.findConversation(props.id))
const history = computed(() => messages.historyOf(props.id))
const isUnknown = computed(() => rooms.status === 'ready' && !conversation.value)

/** Display name for a typing user: the DM peer, or the latest visible author of the room. */
function nameOf(userId: UserId): string {
  const current = conversation.value
  if (current?.kind === 'direct' && current.peer.id === userId) return current.peer.displayName
  const latest = history.value.items.filter((message) => message.author.id === userId).at(-1)
  return latest?.author.displayName ?? NAME_OF_UNKNOWN_AUTHOR
}

const typingText = computed(() => {
  const names = typing.typingUserIds(props.id).map(nameOf)
  if (names.length === 0) return ''
  if (names.length === 1) return `${names[0]} печатает…`
  return 'Несколько человек печатают…'
})

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

// Joined while the chat is open, left when the route changes or the view unmounts.
// A refused join (not_found, C3) is shown by the REST load below, so the error is ignored here.
watch(
  () => props.id,
  (id, _previousId, onCleanup) => {
    void messages.loadLatest(id)
    void realtime.openConversation(id).catch(() => undefined)
    onCleanup(() => void realtime.closeConversation(id))
  },
  { immediate: true },
)

async function send(text: string): Promise<void> {
  await realtime.sendMessage(props.id, text)
}

// Navigate first, then forget the room: the view must not outlive its room in the store.
// Leaving unmounts this view, and the watch cleanup above closes its hub conversation.
async function leftRoom(roomId: string): Promise<void> {
  await router.push({ name: ROUTE_NAMES.chatList })
  rooms.forgetRoom(roomId)
}

const notifyTyping = () => realtime.notifyTyping(props.id)
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
      <LeaveRoomButton v-if="conversation?.kind === 'room'" :room-id="id" @left="leftRoom" />
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
      <TypingIndicator :text="typingText" />
      <MessageComposer :send="send" :unavailable="isSendingUnavailable" @typing="notifyTyping" />
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
