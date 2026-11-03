<script setup lang="ts">
import { REACTION_EMOJIS } from '@/api/reactionCatalog'
import type { PickerPlacement } from '@/utils/pickerPlacement'

defineProps<{
  /** The emoji the user holds on this message now, or null. */
  chosen: string | null
  placement: PickerPlacement
  /** The side of the message the panel lines up with: the start for others' messages, the end for own ones. */
  align: 'start' | 'end'
}>()

const emit = defineEmits<{ choose: [emoji: string] }>()
</script>

<template>
  <div class="picker" :class="[placement, align]" role="group" aria-label="Выберите реакцию">
    <button
      v-for="emoji in REACTION_EMOJIS"
      :key="emoji"
      type="button"
      class="option"
      :class="{ chosen: emoji === chosen }"
      :aria-pressed="emoji === chosen"
      @click="emit('choose', emoji)"
    >
      {{ emoji }}
    </button>
  </div>
</template>

<style scoped>
.picker {
  position: absolute;
  z-index: 2;
  display: flex;
  flex-wrap: nowrap;
  gap: 2px;
  /* Its own width, not the width of the message: a short message must not squeeze the row into two lines. */
  width: max-content;
  max-width: calc(100vw - 32px);
  padding: 4px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface);
  box-shadow: 0 4px 16px rgb(0 0 0 / 18%);
}

.picker.start {
  left: 0;
}

.picker.end {
  right: 0;
}

.picker.above {
  bottom: calc(100% + 4px);
}

.picker.below {
  top: calc(100% + 4px);
}

.option {
  flex: none;
  width: 36px;
  height: 36px;
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: transparent;
  font-size: 1.25rem;
  line-height: 1;
  cursor: pointer;
}

.option:hover {
  background: var(--color-surface-hover);
}

.option.chosen {
  background: var(--color-accent-soft);
}
</style>
