<script setup lang="ts">
import { useRouter } from 'vue-router'

import RoomMembersList from '@/components/RoomMembersList.vue'
import type { ConversationId } from '@/api'
import { ROUTE_NAMES } from '@/router/routes'

defineProps<{ roomId: ConversationId }>()

const router = useRouter()

function openDirect(conversationId: ConversationId): void {
  void router.push({ name: ROUTE_NAMES.chat, params: { id: conversationId } })
}
</script>

<template>
  <aside class="participants" aria-labelledby="participants-title" data-testid="participants-panel">
    <h2 id="participants-title">Участники</h2>
    <RoomMembersList :room-id="roomId" @opened="openDirect" />
  </aside>
</template>

<style scoped>
.participants {
  flex: none;
  width: var(--participants-width);
  padding: 12px 16px;
  overflow-y: auto;
  border-left: 1px solid var(--color-border);
}

h2 {
  margin: 0 0 8px;
  color: var(--color-muted);
  font-size: 0.8rem;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
</style>
