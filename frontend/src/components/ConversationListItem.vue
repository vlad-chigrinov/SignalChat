<script setup lang="ts">
import { computed } from 'vue'

import type { Conversation } from '@/api'
import { ROUTE_NAMES } from '@/router/routes'
import { usePresenceStore } from '@/stores/presence'
import { formatMessageTime } from '@/utils/formatTime'

import PresenceDot from './PresenceDot.vue'
import UserAvatar from './UserAvatar.vue'

const props = defineProps<{ conversation: Conversation }>()

const presence = usePresenceStore()

const title = computed(() =>
  props.conversation.kind === 'room'
    ? `# ${props.conversation.name}`
    : props.conversation.peer.displayName,
)
const preview = computed(() => props.conversation.lastMessage?.text ?? 'Сообщений пока нет')
const sentAt = computed(() => {
  const last = props.conversation.lastMessage
  return last ? formatMessageTime(last.sentAt) : ''
})
</script>

<template>
  <RouterLink class="item" :to="{ name: ROUTE_NAMES.chat, params: { id: conversation.id } }">
    <UserAvatar v-if="conversation.kind === 'direct'" :user="conversation.peer" />
    <span v-else class="room-icon" aria-hidden="true">#</span>
    <span class="body">
      <span class="title-row">
        <span class="title">{{ title }}</span>
        <PresenceDot
          v-if="conversation.kind === 'direct'"
          :online="presence.isOnline(conversation.peer.id)"
        />
        <time class="time">{{ sentAt }}</time>
      </span>
      <span class="preview">{{ preview }}</span>
    </span>
  </RouterLink>
</template>

<style scoped>
.item {
  display: flex;
  gap: 12px;
  align-items: center;
  padding: 10px 12px;
  border-radius: 8px;
  color: inherit;
  text-decoration: none;
}

.item:hover,
.item:focus-visible {
  background: var(--color-surface-hover);
}

.room-icon {
  display: inline-flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: var(--color-surface-hover);
  color: var(--color-muted);
  font-weight: 700;
}

.body {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
}

.title-row {
  display: flex;
  gap: 6px;
  align-items: center;
}

.title {
  overflow: hidden;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.time {
  margin-left: auto;
  color: var(--color-muted);
  font-size: 0.75rem;
  white-space: nowrap;
}

.preview {
  overflow: hidden;
  color: var(--color-muted);
  font-size: 0.875rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
