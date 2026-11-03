<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'

import { MAX_PHOTOS_PER_MESSAGE, PHOTO_ALLOWED_TYPES } from '@/api/limits'
import { addPhotos, removePhoto } from '@/utils/photoSelection'

const props = defineProps<{ files: File[]; disabled?: boolean }>()
const emit = defineEmits<{ 'update:files': [files: File[]] }>()

const ACCEPT = PHOTO_ALLOWED_TYPES.join(',')

const input = ref<HTMLInputElement | null>(null)
const notice = ref<string | null>(null)
/** Local previews only: they are the browser's own copy of the file, nothing is uploaded here. */
const previewUrls = new Map<File, string>()
const previews = ref<{ file: File; url: string }[]>([])

function previewOf(file: File): string {
  let url = previewUrls.get(file)
  if (url === undefined) {
    url = URL.createObjectURL(file)
    previewUrls.set(file, url)
  }
  return url
}

function releaseUnused(current: readonly File[]): void {
  for (const [file, url] of previewUrls) {
    if (current.includes(file)) continue
    URL.revokeObjectURL(url)
    previewUrls.delete(file)
  }
}

watch(
  () => props.files,
  (files) => {
    // The message belongs to the pick that caused it: once the photos are gone (sent), it goes too.
    if (files.length === 0) notice.value = null
    previews.value = files.map((file) => ({ file, url: previewOf(file) }))
    releaseUnused(files)
  },
  { immediate: true },
)

onBeforeUnmount(() => releaseUnused([]))

function openPicker(): void {
  input.value?.click()
}

function onPicked(event: Event): void {
  const target = event.target as HTMLInputElement
  const picked = Array.from(target.files ?? [])
  // The same file can be picked again after it was removed, so the input is cleared each time.
  target.value = ''
  if (picked.length === 0) return
  const selection = addPhotos(props.files, picked)
  notice.value = selection.notice
  emit('update:files', selection.files)
}

function remove(index: number): void {
  notice.value = null
  emit('update:files', removePhoto(props.files, index))
}
</script>

<template>
  <div class="picker">
    <ul v-if="previews.length > 0" class="previews" aria-label="Выбранные фото">
      <li v-for="(preview, index) in previews" :key="preview.url" class="preview">
        <img class="thumb" :src="preview.url" :alt="`Фото ${index + 1}: ${preview.file.name}`" />
        <button
          type="button"
          class="remove"
          :aria-label="`Убрать фото ${index + 1}`"
          :disabled="disabled"
          @click="remove(index)"
        >
          ×
        </button>
      </li>
    </ul>
    <input
      ref="input"
      class="file-input"
      type="file"
      :accept="ACCEPT"
      multiple
      tabindex="-1"
      aria-hidden="true"
      data-testid="photo-input"
      @change="onPicked"
    />
    <button
      type="button"
      class="attach"
      aria-label="Прикрепить фото"
      :title="`Прикрепить фото (до ${MAX_PHOTOS_PER_MESSAGE})`"
      :disabled="disabled || files.length >= MAX_PHOTOS_PER_MESSAGE"
      @click="openPicker"
    >
      <svg
        class="icon"
        viewBox="0 0 24 24"
        width="22"
        height="22"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
        focusable="false"
      >
        <rect x="3" y="4" width="18" height="16" rx="3" />
        <circle cx="9" cy="10" r="1.8" />
        <path d="M3.5 17.5 9 12.5l4 3.6 3-2.6 4.5 4" />
      </svg>
    </button>
    <p v-if="notice" class="notice" role="alert">{{ notice }}</p>
  </div>
</template>

<style scoped>
/* The parts take part in the composer's own flex row. */
.picker {
  display: contents;
}

.file-input {
  display: none;
}

.previews {
  display: flex;
  flex-basis: 100%;
  flex-wrap: wrap;
  gap: 8px;
  order: -1;
  margin: 0;
  padding: 6px 6px 2px;
  list-style: none;
}

.preview {
  position: relative;
  width: 64px;
  height: 64px;
}

.thumb {
  width: 100%;
  height: 100%;
  border-radius: 8px;
  object-fit: cover;
}

.remove {
  position: absolute;
  top: -6px;
  right: -6px;
  width: 22px;
  height: 22px;
  padding: 0;
  border: 1px solid var(--color-border);
  border-radius: 50%;
  background: var(--color-surface);
  color: inherit;
  cursor: pointer;
  line-height: 1;
}

/* A round icon button without its own frame: the composer's bar is the frame. */
.attach {
  display: inline-flex;
  flex: none;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: transparent;
  color: var(--color-muted);
  cursor: pointer;
}

.attach:hover:not(:disabled),
.attach:focus-visible {
  background: var(--color-surface-hover);
  color: var(--color-accent);
}

.icon {
  display: block;
}

.attach:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.notice {
  flex-basis: 100%;
  order: 1;
  margin: 0;
  padding: 0 10px 4px;
  color: var(--color-danger);
  font-size: 0.875rem;
}
</style>
