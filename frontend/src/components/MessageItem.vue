<script setup lang="ts">
import { ref } from 'vue'

import type { Message } from '@/api'
import { useDismissOnOutside } from '@/composables/useDismissOnOutside'
import { useMessagesStore } from '@/stores/messages'
import { errorMessage } from '@/utils/errorMessage'
import { formatMessageTime } from '@/utils/formatTime'
import { pickerPlacement, type PickerPlacement } from '@/utils/pickerPlacement'
import { ownEmojiOf } from '@/utils/reactions'

import ReactionBar from './ReactionBar.vue'
import ReactionPicker from './ReactionPicker.vue'
import UserAvatar from './UserAvatar.vue'

/** A press on one of these keeps its own meaning: it never opens the reaction panel. */
const INTERACTIVE_SELECTOR = 'a, button, input, textarea, select, label'
const MESSAGE_LIST_SELECTOR = '[role="log"]'

const props = defineProps<{ message: Message; isOwn: boolean }>()

const messages = useMessagesStore()

const column = ref<HTMLElement | null>(null)
const reactButton = ref<HTMLButtonElement | null>(null)
const isPickerOpen = ref(false)
const placement = ref<PickerPlacement>('above')
const reactionError = ref<string | null>(null)

useDismissOnOutside(isPickerOpen, column, (reason) => {
  isPickerOpen.value = false
  // Escape leaves the keyboard user where they were; a press elsewhere keeps its own target.
  if (reason === 'escape') reactButton.value?.focus()
})

function togglePicker(): void {
  if (isPickerOpen.value) {
    isPickerOpen.value = false
    return
  }
  placement.value = placementFor(column.value)
  isPickerOpen.value = true
}

function placementFor(element: HTMLElement | null): PickerPlacement {
  const list = element?.closest(MESSAGE_LIST_SELECTOR)
  if (!element || !list) return 'above'
  return pickerPlacement(element.getBoundingClientRect().top - list.getBoundingClientRect().top)
}

/**
 * A press on the message opens the panel, unless it was the end of a text selection or a press on
 * a link or a control: reading, copying and links keep working as before.
 */
function onBubbleClick(event: MouseEvent): void {
  const target = event.target
  if (target instanceof Element && target.closest(INTERACTIVE_SELECTOR)) return
  if (window.getSelection()?.toString()) return
  togglePicker()
}

async function choose(emoji: string): Promise<void> {
  isPickerOpen.value = false
  reactionError.value = null
  try {
    await messages.chooseReaction(props.message.conversationId, props.message.id, emoji)
  } catch (caught) {
    reactionError.value = errorMessage(caught)
  }
}
</script>

<template>
  <article class="message" :class="{ own: isOwn }">
    <UserAvatar v-if="!isOwn" :user="message.author" :size="32" />
    <div ref="column" class="column">
      <div class="bubble" @click="onBubbleClick">
        <header class="meta">
          <span v-if="!isOwn" class="author">{{ message.author.displayName }}</span>
          <time :datetime="message.sentAt">{{ formatMessageTime(message.sentAt) }}</time>
          <button
            ref="reactButton"
            type="button"
            class="react-button"
            aria-label="Добавить реакцию"
            aria-haspopup="true"
            :aria-expanded="isPickerOpen"
            @click="togglePicker"
          >
            ☺
          </button>
        </header>
        <!-- Plain text interpolation only: never v-html (XSS). -->
        <p class="text">{{ message.text }}</p>
      </div>
      <ReactionPicker
        v-if="isPickerOpen"
        :chosen="ownEmojiOf(message.reactions)"
        :placement="placement"
        :align="isOwn ? 'end' : 'start'"
        @choose="choose"
      />
      <ReactionBar :reactions="message.reactions" @choose="choose" />
      <p v-if="reactionError" class="reaction-error" role="status">{{ reactionError }}</p>
    </div>
  </article>
</template>

<style scoped>
.message {
  display: flex;
  gap: 8px;
  align-items: flex-end;
  max-width: min(80%, 640px);
}

.message.own {
  align-self: flex-end;
}

.column {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  min-width: 0;
}

.own .column {
  align-items: flex-end;
}

.bubble {
  min-width: 0;
  padding: 8px 12px;
  border-radius: 12px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  cursor: pointer;
}

.own .bubble {
  background: var(--color-accent-soft);
  border-color: transparent;
}

.meta {
  display: flex;
  gap: 8px;
  align-items: baseline;
  color: var(--color-muted);
  font-size: 0.75rem;
}

.author {
  color: var(--color-accent);
  font-weight: 600;
}

/* Shown on hover and focus; the whole bubble is the target for mouse and touch, this one is for the keyboard. */
.react-button {
  margin-left: auto;
  padding: 0 4px;
  border: 0;
  background: transparent;
  color: inherit;
  font-size: 0.9rem;
  line-height: 1;
  opacity: 0;
  cursor: pointer;
}

.message:hover .react-button,
.react-button:focus-visible,
.react-button[aria-expanded='true'] {
  opacity: 1;
}

.text {
  margin: 2px 0 0;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
}

.reaction-error {
  margin: 4px 0 0;
  color: var(--color-danger);
  font-size: 0.75rem;
}
</style>
