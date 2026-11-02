import {
  AVATAR_ALLOWED_TYPES,
  AVATAR_MAX_UPLOAD_BYTES,
  DISPLAY_NAME_MAX_LENGTH,
} from '@/api/limits'

const EMPTY_NAME_TEXT = 'Введите имя.'
const LONG_NAME_TEXT = `Имя должно быть не длиннее ${DISPLAY_NAME_MAX_LENGTH} символов.`
const FORBIDDEN_CHARACTERS_TEXT = 'В имени есть недопустимые символы.'
const UNSUPPORTED_FILE_TEXT = 'Подойдёт картинка JPEG, PNG, GIF или WebP.'
const LARGE_FILE_TEXT = 'Файл больше 2 МБ.'

/** Control characters, line and paragraph separators and the bidirectional overrides: the server refuses the same. */
const FORBIDDEN_NAME_CHARACTERS = /[\p{Cc}\p{Zl}\p{Zp}\u{202A}-\u{202E}\u{2066}-\u{2069}]/u

/** Client-side check of the name; the server applies the same rules and has the last word. */
export function displayNameError(name: string): string | null {
  const trimmed = name.trim()
  if (trimmed === '') return EMPTY_NAME_TEXT
  if (trimmed.length > DISPLAY_NAME_MAX_LENGTH) return LONG_NAME_TEXT
  if (FORBIDDEN_NAME_CHARACTERS.test(trimmed)) return FORBIDDEN_CHARACTERS_TEXT
  return null
}

/**
 * Checks the picked file before it is sent. The type here is what the browser reports, which only saves a round trip:
 * the server decides by the content of the file.
 */
export function avatarFileError(file: Pick<Blob, 'size' | 'type'>): string | null {
  if (!AVATAR_ALLOWED_TYPES.includes(file.type)) return UNSUPPORTED_FILE_TEXT
  if (file.size > AVATAR_MAX_UPLOAD_BYTES) return LARGE_FILE_TEXT
  return null
}

/** A Google avatar is an absolute https address (the server keeps no other); an uploaded one is served by the API. */
export function isUploadedAvatar(avatarUrl: string | null): boolean {
  return avatarUrl !== null && !avatarUrl.startsWith('https://')
}
