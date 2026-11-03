import { describe, expect, it } from 'vitest'

import { draftAfterFailedSend } from '../composerDraft'

describe('draftAfterFailedSend', () => {
  it('puts the failed text back into an empty field as it was', () => {
    expect(draftAfterFailedSend('привет\n мир ', '')).toBe('привет\n мир ')
  })

  it('treats a field with only whitespace as empty', () => {
    expect(draftAfterFailedSend('привет', ' \n ')).toBe('привет')
  })

  it('keeps what the user typed meanwhile, after the failed text on its own line', () => {
    expect(draftAfterFailedSend('первое', 'второе')).toBe('первое\nвторое')
  })

  it('does not leave a blank line when the failed text ends with a line break', () => {
    expect(draftAfterFailedSend('первое\n', 'второе')).toBe('первое\nвторое')
  })
})
