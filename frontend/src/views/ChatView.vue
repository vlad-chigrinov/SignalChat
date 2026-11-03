<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import InviteToRoomDialog from '@/components/InviteToRoomDialog.vue'
import LeaveRoomButton from '@/components/LeaveRoomButton.vue'
import MessageComposer from '@/components/MessageComposer.vue'
import MessageList from '@/components/MessageList.vue'
import PresenceDot from '@/components/PresenceDot.vue'
import RoomMembersDialog from '@/components/RoomMembersDialog.vue'
import TypingIndicator from '@/components/TypingIndicator.vue'
import { getRealtimeService } from '@/realtime'
import { ROUTE_NAMES } from '@/router/routes'
import { useAuthStore } from '@/stores/auth'
import { useConnectionStore } from '@/stores/connection'
import { useMessagesStore } from '@/stores/messages'
import { usePresenceStore } from '@/stores/presence'
import { useRoomNoticesStore } from '@/stores/roomNotices'
import { useRoomsStore } from '@/stores/rooms'
import { useTypingStore } from '@/stores/typing'
import type { ConversationId, UserId } from '@/api'

const NAME_OF_UNKNOWN_AUTHOR = 'Кто-то'

const props = defineProps<{ id: string }>()

const auth = useAuthStore()
const rooms = useRoomsStore()
const messages = useMessagesStore()
const roomNotices = useRoomNoticesStore()
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

/** A request this user wrote that the peer has not answered: read-only until it is accepted. */
const isAwaitingAnswer = computed(
  () => conversation.value?.kind === 'direct' && conversation.value.status === 'pending',
)

const subtitle = computed(() => {
  const current = conversation.value
  if (!current) return ''
  if (current.kind === 'room') return `${current.description} · участников: ${current.memberCount}`
  if (current.status === 'pending') return 'Ожидает ответа'
  return presence.isOnline(current.peer.id) ? 'в сети' : 'не в сети'
})

const composerBlockedReason = computed(() =>
  isAwaitingAnswer.value ? 'Ожидает ответа собеседника.' : 'Нет соединения. Отправка недоступна.',
)

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

const isMembersOpen = ref(false)
const isInviteOpen = ref(false)

/** "Написать" in the member list: the chat opens (or the new request is shown as pending) and the dialog goes. */
async function openDirect(conversationId: ConversationId): Promise<void> {
  isMembersOpen.value = false
  await router.push({ name: ROUTE_NAMES.chat, params: { id: conversationId } })
}
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
      <div v-if="conversation?.kind === 'room'" class="room-actions">
        <button type="button" class="button secondary" @click="isMembersOpen = true">
          Участники
        </button>
        <button type="button" class="button secondary" @click="isInviteOpen = true">
          Пригласить
        </button>
        <LeaveRoomButton :room-id="id" @left="leftRoom" />
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
        :notices="roomNotices.noticesOf(id)"
        @load-older="messages.loadOlder(id)"
      />
      <TypingIndicator :text="typingText" />
      <MessageComposer
        :send="send"
        :unavailable="isSendingUnavailable || isAwaitingAnswer"
        :unavailable-reason="composerBlockedReason"
        @typing="notifyTyping"
      />
    </template>

    <RoomMembersDialog
      v-if="isMembersOpen"
      :room-id="id"
      @close="isMembersOpen = false"
      @opened="openDirect"
    />
    <InviteToRoomDialog v-if="isInviteOpen" :room-id="id" @close="isInviteOpen = false" />
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

.room-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: flex-start;
  margin-left: auto;
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
