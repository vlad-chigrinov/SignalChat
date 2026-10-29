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

  it('takes the last known code when details follow the code', () => {
    expect(parseHubErrorCode('Error: not_found\n   at ChatHub.JoinRoom')).toBe('not_found')
  })

  it('no longer knows "forbidden": a non-member is always not_found (C3)', () => {
    expect(parseHubErrorCode('Error: forbidden')).toBe('unknown')
  })

  it('is case-insensitive', () => {
    expect(parseHubErrorCode('Error: RATE_LIMITED')).toBe('rate_limited')
  })

  it('falls back to unknown for unrecognised text', () => {
    expect(parseHubErrorCode('Error: database connection lost')).toBe('unknown')
    expect(parseHubErrorCode('')).toBe('unknown')
  })

  it('does not match a code embedded inside a longer word', () => {
    expect(parseHubErrorCode('Error: not_foundness')).toBe('unknown')
  })
})
