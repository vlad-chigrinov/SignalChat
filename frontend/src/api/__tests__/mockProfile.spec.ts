import { describe, expect, it } from 'vitest'

import { AVATAR_MAX_UPLOAD_BYTES, DISPLAY_NAME_MAX_LENGTH } from '../limits'
import { ApiError } from '../http'
import { createSeedStore, mockCurrentUser } from '../mock/mockData'
import { changeDisplayName, resetAvatar, uploadAvatar } from '../mock/mockProfile'

/** Asserts that the rule throws an ApiError with the given stable code. */
function expectCode(action: () => unknown, code: string): void {
  let caught: unknown = null
  try {
    action()
  } catch (error) {
    caught = error
  }
  expect(caught).toBeInstanceOf(ApiError)
  expect((caught as ApiError).code).toBe(code)
}

describe('mock profile rules (same as the server)', () => {
  it('trims and stores the new name', () => {
    const store = createSeedStore()

    const updated = changeDisplayName(store, '  Новое имя  ')

    expect(updated.displayName).toBe('Новое имя')
    expect(store.currentUser.displayName).toBe('Новое имя')
    expect(updated.email).toBe(mockCurrentUser.email)
  })

  it('does not touch the shared demo user, so the next test starts clean', () => {
    changeDisplayName(createSeedStore(), 'Чужое имя')

    expect(createSeedStore().currentUser.displayName).toBe(mockCurrentUser.displayName)
  })

  it('refuses an empty, too long or control-character name with invalid_display_name', () => {
    const store = createSeedStore()

    expectCode(() => changeDisplayName(store, '   '), 'invalid_display_name')
    expectCode(
      () => changeDisplayName(store, 'я'.repeat(DISPLAY_NAME_MAX_LENGTH + 1)),
      'invalid_display_name',
    )
    expectCode(() => changeDisplayName(store, 'две\nстроки'), 'invalid_display_name')
    expect(store.currentUser.displayName).toBe(mockCurrentUser.displayName)
  })

  it('shows the new name on the messages the user wrote', () => {
    const store = createSeedStore()

    changeDisplayName(store, 'Новое имя')

    const own = [...store.histories.values()]
      .flat()
      .filter((m) => m.author.id === mockCurrentUser.id)
    expect(own.length).toBeGreaterThan(0)
    expect(own.every((message) => message.author.displayName === 'Новое имя')).toBe(true)
  })

  it('takes a picture as the avatar and returns to the Google one', () => {
    const store = createSeedStore()

    const uploaded = uploadAvatar(store, new Blob(['x'], { type: 'image/png' }))
    const reset = resetAvatar(store)

    expect(uploaded.avatarUrl).toMatch(/^blob:/)
    expect(reset.avatarUrl).toBe(mockCurrentUser.avatarUrl)
  })

  it('refuses a file that is not a supported picture, or is too large', () => {
    const store = createSeedStore()

    expectCode(
      () => uploadAvatar(store, new Blob(['<svg/>'], { type: 'image/svg+xml' })),
      'invalid_avatar',
    )
    expectCode(
      () =>
        uploadAvatar(
          store,
          new Blob([new Uint8Array(AVATAR_MAX_UPLOAD_BYTES + 1)], { type: 'image/png' }),
        ),
      'avatar_too_large',
    )
  })
})
