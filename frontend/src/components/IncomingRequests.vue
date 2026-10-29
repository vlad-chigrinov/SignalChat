<script setup lang="ts">
import { onMounted, ref } from 'vue'

import UserAvatar from '@/components/UserAvatar.vue'
import type { ConversationId } from '@/api'
import { useDirectRequestsStore } from '@/stores/directRequests'
import { errorMessage } from '@/utils/errorMessage'

/** Accepted: the parent opens the new chat. */
const emit = defineEmits<{ accepted: [conversationId: ConversationId] }>()

const requests = useDirectRequestsStore()
const error = ref<string | null>(null)
/** The request whose answer is on its way: its buttons are blocked meanwhile. */
const busyId = ref<ConversationId | null>(null)

onMounted(() => {
  requests.load().catch((caught: unknown) => {
    error.value = errorMessage(caught)
  })
})

/** Runs one answer; false when it failed, with the text shown in this list. */
async function answer(
  conversationId: ConversationId,
  action: () => Promise<void>,
): Promise<boolean> {
  error.value = null
  busyId.value = conversationId
  try {
    await action()
    return true
  } catch (caught) {
    error.value = errorMessage(caught)
    return false
  } finally {
    busyId.value = null
  }
}

async function accept(conversationId: ConversationId): Promise<void> {
  if (await answer(conversationId, () => requests.accept(conversationId))) {
    emit('accepted', conversationId)
  }
}

async function decline(conversationId: ConversationId): Promise<void> {
  await answer(conversationId, () => requests.decline(conversationId))
}
</script>

<template>
  <section aria-labelledby="requests-title">
    <h2 id="requests-title">
      Запросы
      <span v-if="requests.count > 0" class="badge">{{ requests.count }}</span>
    </h2>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <p v-if="requests.count === 0" class="empty">Новых запросов нет.</p>
    <ul v-else>
      <li v-for="request in requests.incoming" :key="request.id" class="request">
        <UserAvatar :user="request.from" />
        <div class="body">
          <p class="name">{{ request.from.displayName }}</p>
          <!-- The text is the sender's: interpolated only, never rendered as HTML. -->
          <p class="text">{{ request.text }}</p>
          <div class="actions">
            <button
              type="button"
              class="button"
              :disabled="busyId !== null"
              @click="accept(request.id)"
            >
              Принять
            </button>
            <button
              type="button"
              class="button secondary"
              :disabled="busyId !== null"
              @click="decline(request.id)"
            >
              Отклонить
            </button>
          </div>
        </div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
h2 {
  display: flex;
  gap: 8px;
  align-items: center;
  margin: 16px 12px 8px;
  color: var(--color-muted);
  font-size: 0.8rem;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.badge {
  min-width: 20px;
  padding: 0 6px;
  border-radius: 999px;
  background: var(--color-accent);
  color: var(--color-on-accent, #fff);
  font-size: 0.75rem;
  line-height: 20px;
  text-align: center;
}

ul {
  margin: 0;
  padding: 0;
  list-style: none;
}

.request {
  display: flex;
  gap: 12px;
  padding: 10px 12px;
}

.body {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.name,
.text {
  margin: 0;
}

.name {
  font-weight: 600;
}

.text {
  overflow-wrap: anywhere;
  white-space: pre-wrap;
}

.actions {
  display: flex;
  gap: 8px;
  margin-top: 4px;
}

.empty,
.error {
  margin: 0;
  padding: 0 12px;
}

.empty {
  color: var(--color-muted);
}

.error {
  color: var(--color-danger);
}
</style>
