/** Upper bounds the client applies before it calls the server, so a request never exceeds them. */
export const DISCOVER_MAX_LIMIT = 50
export const USER_SEARCH_MAX_LIMIT = 20
export const HISTORY_DEFAULT_LIMIT = 30
export const HISTORY_MAX_LIMIT = 100

/** Profile limits. The server checks the same ones; the client only saves a round trip. */
export const DISPLAY_NAME_MAX_LENGTH = 100
const BYTES_PER_MEBIBYTE = 1024 * 1024
export const AVATAR_MAX_UPLOAD_BYTES = 2 * BYTES_PER_MEBIBYTE
/** The picture types the server decodes. The server decides by content; this is only the file picker's hint. */
export const AVATAR_ALLOWED_TYPES: readonly string[] = [
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/webp',
]

/** Photo limits. The server checks the same ones (and the content); the client only saves a round trip. */
export const MAX_PHOTOS_PER_MESSAGE = 9
export const PHOTO_MAX_FILE_BYTES = 12 * BYTES_PER_MEBIBYTE
/** What the picker offers and the client accepts; the server decides by content. */
export const PHOTO_ALLOWED_TYPES: readonly string[] = AVATAR_ALLOWED_TYPES

/** Keeps a requested page size within 1..max. Non-integers are rounded down. */
export function clampLimit(requested: number, max: number): number {
  if (!Number.isFinite(requested)) return max
  return Math.min(Math.max(Math.trunc(requested), 1), max)
}
