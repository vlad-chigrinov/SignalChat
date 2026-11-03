<script setup lang="ts">
import type { Reaction } from '@/api'
import { reactionCountLabel } from '@/utils/reactions'

defineProps<{ reactions: Reaction[] }>()

const emit = defineEmits<{ choose: [emoji: string] }>()
</script>

<template>
  <ul v-if="reactions.length > 0" class="reactions" aria-label="Реакции">
    <li v-for="reaction in reactions" :key="reaction.emoji">
      <!-- A chip is a button: pressing the own one takes the reaction back. Emoji and number are plain text. -->
      <button
        type="button"
        class="chip"
        :class="{ mine: reaction.reactedByMe }"
        :aria-pressed="reaction.reactedByMe"
        :title="reaction.reactedByMe ? 'Убрать реакцию' : 'Поставить такую же реакцию'"
        @click="emit('choose', reaction.emoji)"
      >
        <span class="emoji">{{ reaction.emoji }}</span>
        <span v-if="reactionCountLabel(reaction.count)" class="count">
          {{ reactionCountLabel(reaction.count) }}
        </span>
      </button>
    </li>
  </ul>
</template>

<style scoped>
.reactions {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin: 4px 0 0;
  padding: 0;
  list-style: none;
}

.chip {
  display: inline-flex;
  gap: 4px;
  align-items: center;
  min-height: 28px;
  padding: 2px 8px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 0.85rem;
  cursor: pointer;
}

.chip:hover {
  background: var(--color-surface-hover);
}

.chip.mine {
  border-color: var(--color-accent);
  background: var(--color-accent-soft);
  font-weight: 600;
}

.count {
  font-variant-numeric: tabular-nums;
}
</style>
