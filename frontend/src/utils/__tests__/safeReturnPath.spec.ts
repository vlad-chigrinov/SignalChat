import { describe, expect, it } from 'vitest'

import { safeReturnPath } from '../safeReturnPath'

describe('safeReturnPath', () => {
  it('keeps same-origin relative paths', () => {
    expect(safeReturnPath('/chat/room-general?x=1')).toBe('/chat/room-general?x=1')
  })

  it.each(['https://evil.example', '//evil.example', '/\\evil.example', 'chat', '', undefined, 42])(
    'falls back to / for %s',
    (candidate) => {
      expect(safeReturnPath(candidate)).toBe('/')
    },
  )
})
