import { type HubErrorCode, isHubErrorCode } from './hubContract'

const CODE_SEPARATOR = ': '

/**
 * Maps the text of a server `HubException` to its stable code (docs/realtime-hub.md): the code
 * is the text after the last ": " ("... HubException: not_found"). Anything else becomes
 * `unknown`, so the UI never shows raw server text.
 */
export function parseHubErrorCode(message: string): HubErrorCode {
  const separatorIndex = message.lastIndexOf(CODE_SEPARATOR)
  const candidate = (
    separatorIndex === -1 ? message : message.slice(separatorIndex + CODE_SEPARATOR.length)
  ).trim()
  return isHubErrorCode(candidate) ? candidate : 'unknown'
}
