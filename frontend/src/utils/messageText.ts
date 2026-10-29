export const MAX_MESSAGE_LENGTH = 2000

/**
 * Trims a draft and checks the length limit shared with the backend.
 * Throws `RangeError` for an empty or too long message.
 */
export function normalizeMessageText(rawText: string): string {
  const text = rawText.trim()
  if (text.length === 0 || text.length > MAX_MESSAGE_LENGTH) {
    throw new RangeError(`Message must be 1..${MAX_MESSAGE_LENGTH} characters`)
  }
  return text
}
