import { describe, expect, it } from 'vitest'

import { AVATAR_MAX_UPLOAD_BYTES, DISPLAY_NAME_MAX_LENGTH } from '@/api/limits'

import { avatarFileError, displayNameError, isUploadedAvatar } from '../profileForm'

describe('displayNameError', () => {
  it('accepts an ordinary name, with emoji too', () => {
    expect(displayNameError('Анна Смирнова')).toBeNull()
    expect(displayNameError('Рыжий 🦊')).toBeNull()
  })

  it('asks for a name when it is empty or only spaces', () => {
    expect(displayNameError('')).toMatch(/имя/i)
    expect(displayNameError('   ')).toMatch(/имя/i)
  })

  it('counts the name after trimming, up to the server limit', () => {
    expect(displayNameError(` ${'я'.repeat(DISPLAY_NAME_MAX_LENGTH)} `)).toBeNull()
    expect(displayNameError('я'.repeat(DISPLAY_NAME_MAX_LENGTH + 1))).toMatch(/не длиннее/)
  })

  it('refuses control characters', () => {
    expect(displayNameError('две\nстроки')).toMatch(/недопустимые/)
    expect(displayNameError('tab\there')).toMatch(/недопустимые/)
  })
})

describe('avatarFileError', () => {
  it('accepts the picture types the server decodes', () => {
    for (const type of ['image/jpeg', 'image/png', 'image/gif', 'image/webp']) {
      expect(avatarFileError({ type, size: 1000 })).toBeNull()
    }
  })

  it('refuses other types, such as SVG, before sending', () => {
    expect(avatarFileError({ type: 'image/svg+xml', size: 1000 })).toMatch(
      /JPEG, PNG, GIF или WebP/,
    )
    expect(avatarFileError({ type: 'application/pdf', size: 1000 })).toMatch(/JPEG/)
    expect(avatarFileError({ type: '', size: 1000 })).toMatch(/JPEG/)
  })

  it('refuses a file over the size limit', () => {
    expect(avatarFileError({ type: 'image/png', size: AVATAR_MAX_UPLOAD_BYTES })).toBeNull()
    expect(avatarFileError({ type: 'image/png', size: AVATAR_MAX_UPLOAD_BYTES + 1 })).toMatch(
      /2 МБ/,
    )
  })
})

describe('isUploadedAvatar', () => {
  it('tells an avatar served by the API from a Google one', () => {
    expect(isUploadedAvatar('/api/users/u1/avatar?v=2')).toBe(true)
    expect(isUploadedAvatar('blob:http://localhost:5174/1b2c')).toBe(true)
    expect(isUploadedAvatar('https://lh3.googleusercontent.com/a/x')).toBe(false)
    expect(isUploadedAvatar(null)).toBe(false)
  })
})
