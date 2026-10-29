<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import ConversationListItem from '@/components/ConversationListItem.vue'
import CreateRoomDialog from '@/components/CreateRoomDialog.vue'
import UserSearchDialog from '@/components/UserSearchDialog.vue'
import type { ConversationId, DirectConversation, Room } from '@/api'
import { ROUTE_NAMES } from '@/router/routes'
import { useRoomsStore } from '@/stores/rooms'

const rooms = useRoomsStore()
const router = useRouter()
const isCreateRoomOpen = ref(false)
const isUserSearchOpen = ref(false)

onMounted(() => void rooms.load())

function openConversation(id: ConversationId): void {
  void router.push({ name: ROUTE_NAMES.chat, params: { id } })
}

function openRoom(room: Room): void {
  isCreateRoomOpen.value = false
  openConversation(room.id)
}

function openDirect(direct: DirectConversation): void {
  isUserSearchOpen.value = false
  openConversation(direct.id)
}
</script>

<template>
  <main class="chat-list">
    <p v-if="rooms.status === 'loading'" class="state">Загрузка чатов…</p>

    <div v-else-if="rooms.status === 'error'" class="state" role="alert">
      <p>{{ rooms.error }}</p>
      <button type="button" class="button" @click="rooms.load({ force: true })">Повторить</button>
    </div>

    <template v-else>
      <div class="toolbar">
        <RouterLink class="button secondary" :to="{ name: ROUTE_NAMES.discoverRooms }">
          Найти комнату
        </RouterLink>
        <button type="button" class="button secondary" @click="isUserSearchOpen = true">
          Новое сообщение
        </button>
        <button type="button" class="button" @click="isCreateRoomOpen = true">
          Создать комнату
        </button>
      </div>

      <section aria-labelledby="rooms-title">
        <h2 id="rooms-title">Комнаты</h2>
        <p v-if="rooms.sortedRooms.length === 0" class="empty">Вы пока не состоите в комнатах.</p>
        <ul v-else>
          <li v-for="room in rooms.sortedRooms" :key="room.id">
            <ConversationListItem :conversation="room" />
          </li>
        </ul>
      </section>

      <section aria-labelledby="directs-title">
        <h2 id="directs-title">Личные сообщения</h2>
        <p v-if="rooms.sortedDirects.length === 0" class="empty">Личных диалогов пока нет.</p>
        <ul v-else>
          <li v-for="direct in rooms.sortedDirects" :key="direct.id">
            <ConversationListItem :conversation="direct" />
          </li>
        </ul>
      </section>
    </template>

    <CreateRoomDialog
      v-if="isCreateRoomOpen"
      @close="isCreateRoomOpen = false"
      @created="openRoom"
    />
    <UserSearchDialog
      v-if="isUserSearchOpen"
      @close="isUserSearchOpen = false"
      @opened="openDirect"
    />
  </main>
</template>

<style scoped>
.chat-list {
  width: min(100%, 720px);
  margin: 0 auto;
  padding: 16px;
  overflow-y: auto;
}

.toolbar {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
  padding: 0 12px;
}

h2 {
  margin: 16px 12px 8px;
  color: var(--color-muted);
  font-size: 0.8rem;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

ul {
  margin: 0;
  padding: 0;
  list-style: none;
}

.state,
.empty {
  padding: 0 12px;
  color: var(--color-muted);
}
</style>
