import { MAX_PHOTOS_PER_MESSAGE, PHOTO_ALLOWED_TYPES, PHOTO_MAX_FILE_BYTES } from '../limits'
import type { Attachment, ConversationId, Message, UserId } from '../types'

import { mockError } from './mockErrors'
import {
  isActiveMember,
  historyOf,
  nextMockId,
  userSummaryOf,
  type MockStore,
} from './mockHistories'

const MOCK_CAPTION_MAX_LENGTH = 2000
const FALLBACK_SIDE_PX = 800
const MOCK_THUMBNAIL_MAX_SIDE_PX = 640

/** How many photo messages the mock has accepted. Only the e2e tests read it: a pick must not move it. */
export const MOCK_UPLOAD_COUNTER_KEY = '__mockPhotoUploads'

function countUpload(): void {
  const scope = globalThis as unknown as Record<string, number | undefined>
  scope[MOCK_UPLOAD_COUNTER_KEY] = (scope[MOCK_UPLOAD_COUNTER_KEY] ?? 0) + 1
}

interface PictureSize {
  width: number
  height: number
}

/** The picture's own size where the browser can read it; a fixed one elsewhere (unit tests have no decoder). */
async function readSize(file: File): Promise<PictureSize> {
  try {
    const bitmap = await createImageBitmap(file)
    const size = { width: bitmap.width, height: bitmap.height }
    bitmap.close()
    return size
  } catch {
    return { width: FALLBACK_SIDE_PX, height: FALLBACK_SIDE_PX }
  }
}

function thumbnailSize({ width, height }: PictureSize): PictureSize {
  const scale = Math.min(1, MOCK_THUMBNAIL_MAX_SIDE_PX / Math.max(width, height))
  return { width: Math.round(width * scale), height: Math.round(height * scale) }
}

async function toAttachment(file: File, id: string): Promise<Attachment> {
  const size = await readSize(file)
  const thumbnail = thumbnailSize(size)
  // The mock has no server: the browser's own copy of the file stands in for both addresses.
  const url = URL.createObjectURL(file)
  return {
    id,
    width: size.width,
    height: size.height,
    thumbnailWidth: thumbnail.width,
    thumbnailHeight: thumbnail.height,
    sizeBytes: file.size,
    contentType: file.type,
    fileName: file.name,
    url,
    thumbnailUrl: url,
  }
}

function checkFiles(photos: readonly File[]): void {
  if (photos.length < 1 || photos.length > MAX_PHOTOS_PER_MESSAGE) {
    throw mockError('invalid_attachments')
  }
  for (const photo of photos) {
    if (!PHOTO_ALLOWED_TYPES.includes(photo.type)) throw mockError('invalid_image')
    if (photo.size > PHOTO_MAX_FILE_BYTES) throw mockError('attachment_too_large')
  }
}

/** The server's rules for a photo message, on the shared mock store; the message is in the history afterwards. */
export async function sendPhotoMessage(
  store: MockStore,
  callerId: UserId,
  conversationId: ConversationId,
  caption: string,
  photos: readonly File[],
): Promise<Message> {
  if (!isActiveMember(store, conversationId, callerId)) throw mockError('not_found')
  checkFiles(photos)
  const text = caption.trim()
  if (text.length > MOCK_CAPTION_MAX_LENGTH) throw mockError('invalid_message')
  const author = userSummaryOf(store, callerId)
  if (!author) throw mockError('not_found')

  const attachments = await Promise.all(
    photos.map((photo) => toAttachment(photo, nextMockId(store, 'att'))),
  )
  const message: Message = {
    id: nextMockId(store, 'msg'),
    conversationId,
    author,
    text,
    sentAt: new Date().toISOString(),
    attachments,
  }
  historyOf(store, conversationId).push(message)
  countUpload()
  return message
}
