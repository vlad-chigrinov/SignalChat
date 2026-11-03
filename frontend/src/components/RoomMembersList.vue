<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import DirectRequestForm from '@/components/DirectRequestForm.vue'
import RoomMemberRow from '@/components/RoomMemberRow.vue'
import type { ConversationId, UserSummary } from '@/api'
import { useDirectRequestSend } from '@/composables/useDirectRequestSend'
import { useAuthStore } from '@/stores/auth'
import { useDirectRequestsStore } from '@/stores/directRequests'
import { usePresenceStore } from '@/stores/presence'
import { useRoomMembersStore } from '@/stores/roomMembers'
import { useRoomsStore } from '@/stores/rooms'
import { API_ERROR_TEXT } from '@/utils/apiErrorText'
import { resolveWriteTarget } from '@/utils/writeTarget'

const props = defineProps<{ roomId: ConversationId }>()
const emit = defineEmits<{ opened: [conversationId: ConversationId] }>()

const auth = useAuthStore()
const rooms = useRoomsStore()
const directRequests = useDirectRequestsStore()
const presence = usePresenceStore()
const members = useRoomMembersStore()
const { isSending, sendError, send, clearError } = useDirectRequestSend()

const entry = computed(() => members.entryOf(props.roomId))
/** The member the user is writing the first message to; null while the list is shown. */
const chosen = ref<UserSummary | null>(null)
/** Why "Написать" did nothing: a request is already waiting, so no second one is created. */
const note = ref<string | null>(null)

// Fresh list, and fresh presence: members who joined after the last snapshot are peers only now.
onMounted(() => {
  void members.load(props.roomId)
  void presence.loadSnapshot()
})

function write(user: UserSummary): void {
  note.value = null
  clearError()
  const target = resolveWriteTarget(user.id, rooms.directConversations, directRequests.incoming)
  if (target.kind === 'open') emit('opened', target.conversationId)
  else if (target.kind === 'pending') note.value = API_ERROR_TEXT.dm_request_pending
  else if (target.kind === 'incoming') note.value = API_ERROR_TEXT.dm_incoming_request_exists
  else chosen.value = user
}

async function sendRequest(text: string): Promise<void> {
  if (chosen.value === null) return
  const direct = await send(chosen.value.id, text)
  if (direct) emit('opened', direct.id)
}
</script>

<template>
  <div class="members-list">
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
      <p v-if="entry.status === 'error'" class="error" role="alert">
        {{ entry.error }}
        <button type="button" class="link-button" @click="members.load(roomId)">Повторить</button>
      </p>
      <p v-else-if="entry.items.length === 0" class="state">Загрузка…</p>
      <p v-if="note" class="note" role="status">{{ note }}</p>

      <ul class="members">
        <li v-for="member in entry.items" :key="member.user.id">
          <RoomMemberRow
            :member="member"
            :is-self="member.user.id === auth.user?.id"
            :is-online="member.user.id === auth.user?.id || presence.isOnline(member.user.id)"
            @write="write(member.user)"
          />
        </li>
      </ul>
    </template>
  </div>
</template>

<style scoped>
.members {
  margin: 0;
  padding: 0;
  list-style: none;
}

.state {
  margin: 0;
  color: var(--color-muted);
}

.note {
  margin: 0 0 12px;
  color: var(--color-muted);
  font-size: 0.9rem;
}

.error {
  margin: 0 0 12px;
  color: var(--color-danger);
  font-size: 0.9rem;
}
</style>
