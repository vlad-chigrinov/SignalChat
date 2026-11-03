<script setup lang="ts">
import { ref } from 'vue'

import type { Message } from '@/api'
import { formatMessageTime } from '@/utils/formatTime'

import MessageAttachments from './MessageAttachments.vue'
import PhotoViewer from './PhotoViewer.vue'
import UserAvatar from './UserAvatar.vue'

defineProps<{ message: Message; isOwn: boolean }>()

/** Index of the photo open in the viewer; null while it is closed. */
const openIndex = ref<number | null>(null)
</script>

<template>
  <article class="message" :class="{ own: isOwn, 'has-photos': message.attachments.length > 0 }">
    <UserAvatar v-if="!isOwn" :user="message.author" :size="32" />
    <div class="bubble">
      <header class="meta">
        <span v-if="!isOwn" class="author">{{ message.author.displayName }}</span>
        <time :datetime="message.sentAt">{{ formatMessageTime(message.sentAt) }}</time>
      </header>
      <!-- Plain text interpolation only: never v-html (XSS). -->
      <MessageAttachments
        v-if="message.attachments.length > 0"
        :attachments="message.attachments"
        @open="openIndex = $event"
      />
      <p v-if="message.text !== ''" class="text">{{ message.text }}</p>
    </div>
    <PhotoViewer
      v-if="openIndex !== null"
      :attachments="message.attachments"
      :start-index="openIndex"
      @close="openIndex = null"
    />
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

.bubble {
  min-width: 0;
  padding: 8px 12px;
  border-radius: 12px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
}

/* A definite width, so the photo grid inside has something to take its percentages of. */
.has-photos .bubble {
  width: min(80vw, 436px);
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

.text {
  margin: 2px 0 0;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
}
</style>
