<script setup lang="ts">
import { computed } from 'vue'

import type { Attachment } from '@/api'
import { attachmentSrc } from '@/api/apiConfig'
import { GRID_COLUMNS, photoGridCells } from '@/utils/photoGrid'

const props = defineProps<{ attachments: Attachment[] }>()
const emit = defineEmits<{ open: [index: number] }>()

const cells = computed(() =>
  photoGridCells(
    props.attachments.length,
    props.attachments.map(({ width, height }) => ({ width, height })),
  ),
)

const photos = computed(() =>
  props.attachments.map((attachment, index) => ({
    attachment,
    cell: cells.value[index],
    src: attachmentSrc(attachment.thumbnailUrl),
  })),
)
</script>

<template>
  <ul
    class="grid"
    :class="{ single: attachments.length === 1 }"
    :style="{ '--grid-columns': GRID_COLUMNS }"
    :aria-label="`Фото: ${attachments.length}`"
  >
    <li
      v-for="(photo, index) in photos"
      :key="photo.attachment.id"
      class="cell"
      :style="{
        gridColumn: `span ${photo.cell?.span ?? GRID_COLUMNS}`,
        aspectRatio: String(photo.cell?.aspectRatio ?? 1),
      }"
    >
      <button
        type="button"
        class="open"
        :aria-label="`Открыть фото ${index + 1} из ${photos.length}`"
        @click="emit('open', index)"
      >
        <img
          v-if="photo.src"
          class="image"
          :src="photo.src"
          :width="photo.attachment.thumbnailWidth"
          :height="photo.attachment.thumbnailHeight"
          :alt="photo.attachment.fileName"
          loading="lazy"
          decoding="async"
        />
      </button>
    </li>
  </ul>
</template>

<style scoped>
.grid {
  display: grid;
  grid-template-columns: repeat(var(--grid-columns), 1fr);
  gap: 4px;
  width: min(100%, 420px);
  margin: 4px 0 0;
  padding: 0;
  list-style: none;
}

/* A lone photo may be a little wider; the bubble still caps it on a narrow screen. */
.grid.single {
  width: min(100%, 480px);
}

.cell {
  min-width: 0;
  overflow: hidden;
  border-radius: 8px;
  background: var(--color-surface-hover);
}

.open {
  display: block;
  width: 100%;
  height: 100%;
  padding: 0;
  border: 0;
  background: none;
  cursor: zoom-in;
}

/* Centre crop: the cell has its final shape before the picture has loaded, so nothing jumps. */
.image {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
}
</style>
