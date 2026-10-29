import { describe, expect, it } from 'vitest'

import { parseHubErrorCode } from '../hubErrors'

describe('parseHubErrorCode', () => {
  it.each([
    ['Error: not_found', 'not_found'],
    ['Error: invalid_message', 'invalid_message'],
    ['Error: rate_limited', 'rate_limited'],
    ['Error: too_many_groups', 'too_many_groups'],
    ['Error: unauthenticated', 'unauthenticated'],
  ])('maps "%s" to %s', (message, code) => {
    expect(parseHubErrorCode(message)).toBe(code)
  })

  it('reads the code after the last ": " of a real HubException message', () => {
    const message =
      "Error: An unexpected error occurred invoking 'JoinRoom' on the server. HubException: not_found"

    expect(parseHubErrorCode(message)).toBe('not_found')
  })

  it('does not take a code word that appears inside the details', () => {
    expect(parseHubErrorCode('Error: database says not_found somewhere')).toBe('unknown')
    expect(parseHubErrorCode('Error: unknown\n   at ChatHub.JoinRoom')).toBe('unknown')
  })

  it('no longer knows "forbidden": a non-member is always not_found (C3)', () => {
    expect(parseHubErrorCode('Error: forbidden')).toBe('unknown')
  })

  it('falls back to unknown for unrecognised or empty text', () => {
    expect(parseHubErrorCode('Error: database connection lost')).toBe('unknown')
    expect(parseHubErrorCode('')).toBe('unknown')
  })

  it('does not match a code embedded inside a longer word', () => {
    expect(parseHubErrorCode('Error: not_foundness')).toBe('unknown')
  })
})
