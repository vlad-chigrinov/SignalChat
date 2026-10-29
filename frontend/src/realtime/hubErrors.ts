import { type HubErrorCode, isHubErrorCode } from './hubContract'

const WORD_SEPARATOR = /[^a-z_]+/i

/**
 * Maps the text of a server `HubException` to a stable code. SignalR wraps the message
 * ("Error: forbidden", possibly followed by details), so the last known code word wins.
 * Anything unrecognised becomes `unknown`: the UI never shows raw server text.
 */
export function parseHubErrorCode(message: string): HubErrorCode {
  const words = message.toLowerCase().split(WORD_SEPARATOR)
  for (let index = words.length - 1; index >= 0; index -= 1) {
    const word = words[index]
    if (word !== undefined && isHubErrorCode(word)) return word
  }
  return 'unknown'
}
