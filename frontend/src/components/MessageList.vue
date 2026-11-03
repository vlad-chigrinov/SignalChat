<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'

import type { Message, UserId } from '@/api'

import { buildTimeline, type RoomNotice } from '@/utils/timeline'

import MessageItem from './MessageItem.vue'
import SystemNotice from './SystemNotice.vue'

/** Distance from the top that triggers loading the previous page. */
const LOAD_OLDER_THRESHOLD_PX = 48
/** Within this distance from the bottom, new messages keep the view pinned to the end. */
const STICK_TO_BOTTOM_THRESHOLD_PX = 80

const props = withDefaults(
  defineProps<{
    messages: Message[]
    hasMore: boolean
    isLoading: boolean
    currentUserId: UserId | null
    /** "Member joined" lines shown among the messages by time; they are not part of the history. */
    notices?: readonly RoomNotice[]
  }>(),
  { notices: () => [] },
)

const timeline = computed(() => buildTimeline(props.messages, props.notices))

const emit = defineEmits<{ loadOlder: [] }>()

const container = ref<HTMLElement | null>(null)

interface ScrollSnapshot {
  scrollHeight: number
  scrollTop: number
  wasNearBottom: boolean
}
let snapshot: ScrollSnapshot | null = null
// Ids rendered after the previous update. A pre-flush watcher already sees the NEW props,
// so the old first/last ids cannot be read there.
let renderedFirstId: string | undefined
let renderedLastId: string | undefined

function requestOlder(): void {
  if (props.hasMore && !props.isLoading) emit('loadOlder')
}

function scrollToBottom(): void {
  const element = container.value
  if (element) element.scrollTop = element.scrollHeight
}

/** A short first page leaves nothing to scroll: keep loading until the view is filled. */
function fillViewport(): void {
  const element = container.value
  if (element && element.scrollHeight <= element.clientHeight) requestOlder()
}

function onScroll(): void {
  const element = container.value
  if (element && element.scrollTop <= LOAD_OLDER_THRESHOLD_PX) requestOlder()
}

// Before the DOM updates: remember where the reader is.
watch(
  () => props.messages,
  () => {
    const element = container.value
    if (!element) return
    const distanceFromBottom = element.scrollHeight - element.scrollTop - element.clientHeight
    snapshot = {
      scrollHeight: element.scrollHeight,
      scrollTop: element.scrollTop,
      wasNearBottom: distanceFromBottom <= STICK_TO_BOTTOM_THRESHOLD_PX,
    }
  },
  { flush: 'pre' },
)

// After the DOM updates: keep the reader's place when older messages are prepended.
watch(
  () => props.messages,
  (current) => {
    const element = container.value
    const previous = snapshot
    snapshot = null
    if (!element) return

    const first = current[0]
    const last = current.at(-1)
    if (!previous || renderedFirstId === undefined) {
      scrollToBottom()
    } else if (first?.id !== renderedFirstId && last?.id === renderedLastId) {
      element.scrollTop = element.scrollHeight - previous.scrollHeight + previous.scrollTop
    } else if (last?.id !== renderedLastId) {
      const isOwn = last?.author.id === props.currentUserId
      if (previous.wasNearBottom || isOwn) scrollToBottom()
    }
    renderedFirstId = first?.id
    renderedLastId = last?.id
    fillViewport()
  },
  { flush: 'post' },
)

// A notice is not a message, so the watchers above do not see it: keep a reader who sits at the end at the end.
let noticeWasNearBottom = false
watch(
  () => props.notices.length,
  () => {
    const element = container.value
    noticeWasNearBottom =
      element !== null &&
      element.scrollHeight - element.scrollTop - element.clientHeight <=
        STICK_TO_BOTTOM_THRESHOLD_PX
  },
  { flush: 'pre' },
)
watch(
  () => props.notices.length,
  () => {
    if (noticeWasNearBottom) scrollToBottom()
  },
  { flush: 'post' },
)

onMounted(async () => {
  await nextTick()
  scrollToBottom()
  fillViewport()
})
</script>

<template>
  <div
    ref="container"
    class="list"
    role="log"
    aria-live="polite"
    aria-label="Сообщения"
    tabindex="0"
    @scroll.passive="onScroll"
  >
    <div class="history-edge">
      <span v-if="isLoading">Загрузка…</span>
      <button v-else-if="hasMore" type="button" class="link-button" @click="requestOlder">
        Показать более ранние
      </button>
      <span v-else-if="messages.length > 0">Начало истории</span>
    </div>

    <p v-if="!isLoading && messages.length === 0" class="empty">
      Сообщений пока нет. Напишите первым.
    </p>

    <template v-for="entry in timeline" :key="entry.key">
      <MessageItem
        v-if="entry.type === 'message'"
        :message="entry.message"
        :is-own="entry.message.author.id === currentUserId"
      />
      <SystemNotice v-else :notice="entry.notice" />
    </template>
  </div>
</template>

<style scoped>
.list {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 8px;
  min-height: 0;
  padding: 12px 16px;
  overflow-y: auto;
  overscroll-behavior: contain;
}

.history-edge {
  align-self: center;
  min-height: 1.5rem;
  color: var(--color-muted);
  font-size: 0.8rem;
}

.empty {
  margin: auto;
  color: var(--color-muted);
}
</style>
