<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import BaseDialog from '@/components/BaseDialog.vue'
import UserAvatar from '@/components/UserAvatar.vue'
import { ApiError, type ConversationId, type DirectConversation } from '@/api'
import { useRoomMembersStore } from '@/stores/roomMembers'
import { useRoomsStore } from '@/stores/rooms'
import { errorMessage } from '@/utils/errorMessage'

const props = defineProps<{ roomId: ConversationId }>()
const emit = defineEmits<{ close: [] }>()

const rooms = useRoomsStore()
const members = useRoomMembersStore()

const entry = computed(() => members.entryOf(props.roomId))
/** Only accepted chats: the server refuses anyone else, so they are not offered. */
const candidates = computed(() =>
  rooms.sortedDirects.filter((direct) => direct.status === 'accepted'),
)
const memberIds = computed(() => new Set(entry.value.items.map((member) => member.user.id)))
/** Invited in this dialog: shown as added, whatever the list says next. */
const addedIds = ref(new Set<string>())
const invitingId = ref<string | null>(null)
const inviteError = ref<string | null>(null)

onMounted(() => void members.load(props.roomId))

function isInRoom(direct: DirectConversation): boolean {
  return memberIds.value.has(direct.peer.id)
}

async function invite(direct: DirectConversation): Promise<void> {
  if (invitingId.value !== null) return
  inviteError.value = null
  invitingId.value = direct.peer.id
  try {
    await members.invite(props.roomId, direct.peer)
    addedIds.value.add(direct.peer.id)
  } catch (error) {
    inviteError.value = errorMessage(error)
    // The person is in the room already: the list was stale, so it is read again.
    if (error instanceof ApiError && error.code === 'already_member')
      await members.load(props.roomId)
  } finally {
    invitingId.value = null
  }
}
</script>

<template>
  <BaseDialog title="Пригласить в комнату" @close="emit('close')">
    <p v-if="entry.status === 'error'" class="error" role="alert">
      {{ entry.error }}
      <button type="button" class="link-button" @click="members.load(roomId)">Повторить</button>
    </p>
    <p v-else-if="entry.status !== 'ready'" class="state">Загрузка…</p>
    <p v-else-if="candidates.length === 0" class="state">
      Пригласить можно тех, с кем у вас есть личный чат. Сначала начните диалог и дождитесь ответа.
    </p>

    <p v-if="inviteError" class="error" role="alert">{{ inviteError }}</p>

    <ul v-if="entry.status === 'ready'" class="candidates">
      <li v-for="direct in candidates" :key="direct.id" class="candidate">
        <UserAvatar :user="direct.peer" :size="36" />
        <span class="name">{{ direct.peer.displayName }}</span>
        <span v-if="addedIds.has(direct.peer.id)" class="status" role="status">Добавлен</span>
        <span v-else-if="isInRoom(direct)" class="status">Уже в комнате</span>
        <button
          v-else
          type="button"
          class="button"
          :disabled="invitingId !== null"
          :aria-label="`Пригласить: ${direct.peer.displayName}`"
          @click="invite(direct)"
        >
          Пригласить
        </button>
      </li>
    </ul>
  </BaseDialog>
</template>

<style scoped>
.candidates {
  margin: 0;
  padding: 0;
  list-style: none;
}

.candidate {
  display: flex;
  gap: 12px;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid var(--color-border);
}

.name {
  flex: 1;
  min-width: 0;
  overflow-wrap: anywhere;
  font-weight: 600;
}

.status,
.state {
  color: var(--color-muted);
  font-size: 0.9rem;
}

.state {
  margin: 0 0 12px;
}

.error {
  margin: 0 0 12px;
  color: var(--color-danger);
  font-size: 0.9rem;
}
</style>
