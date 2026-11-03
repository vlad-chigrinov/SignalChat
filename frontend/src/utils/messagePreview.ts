import type { MessagePreview } from '@/api'

/** What a conversation list shows for its last message: the text, or the photos with their caption. */
export function previewText(preview: Pick<MessagePreview, 'text' | 'photoCount'>): string {
  if (preview.photoCount === 0) return preview.text
  const photos = preview.photoCount === 1 ? 'Фото' : `Фото ×${preview.photoCount}`
  return preview.text === '' ? photos : `${photos}: ${preview.text}`
}
