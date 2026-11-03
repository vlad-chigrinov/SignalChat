import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { api, ApiError, type CurrentUser, type Message } from '@/api'

import { useAuthStore } from '../auth'
import { useMessagesStore } from '../messages'

const ME: CurrentUser = { id: 'u-me', displayName: 'Я', avatarUrl: null, email: 'me@example.com' }

describe('auth store profile actions', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('takes the profile the server returns after a rename', async () => {
    const renamed = { ...ME, displayName: 'Новое имя' }
    const changeDisplayName = vi.spyOn(api, 'changeDisplayName').mockResolvedValue(renamed)
    const auth = useAuthStore()
    auth.user = ME

    await auth.changeDisplayName('  Новое имя ')

    expect(changeDisplayName).toHaveBeenCalledWith('  Новое имя ')
    expect(auth.user).toEqual(renamed)
  })

  it('shows the new name in messages already loaded', async () => {
    vi.spyOn(api, 'changeDisplayName').mockResolvedValue({ ...ME, displayName: 'Новое имя' })
    const auth = useAuthStore()
    auth.user = ME
    const messages = useMessagesStore()
    const own: Message = {
      id: 'm1',
      conversationId: 'room-1',
      author: { id: ME.id, displayName: ME.displayName, avatarUrl: null },
      text: 'привет',
      sentAt: '2026-11-02T10:00:00.000Z',
      attachments: [],
    }
    messages.histories['room-1'] = {
      items: [own],
      nextCursor: null,
      hasLoadedLatest: true,
      isLoading: false,
      error: null,
    }

    await auth.changeDisplayName('Новое имя')

    expect(messages.historyOf('room-1').items[0]?.author.displayName).toBe('Новое имя')
  })

  it('keeps the old profile when the server refuses the name', async () => {
    vi.spyOn(api, 'changeDisplayName').mockRejectedValue(
      new ApiError(400, 'refused', 'invalid_display_name'),
    )
    const auth = useAuthStore()
    auth.user = ME

    await expect(auth.changeDisplayName('')).rejects.toBeInstanceOf(ApiError)

    expect(auth.user).toEqual(ME)
  })

  it('uploads the chosen file and takes the new avatar', async () => {
    const withAvatar = { ...ME, avatarUrl: '/api/users/u-me/avatar?v=1' }
    const uploadAvatar = vi.spyOn(api, 'uploadAvatar').mockResolvedValue(withAvatar)
    const auth = useAuthStore()
    auth.user = ME
    const file = new Blob(['x'], { type: 'image/png' })

    await auth.uploadAvatar(file)

    expect(uploadAvatar).toHaveBeenCalledWith(file)
    expect(auth.user).toEqual(withAvatar)
  })

  it('returns to the Google avatar', async () => {
    const google = { ...ME, avatarUrl: 'https://lh3.googleusercontent.com/a/me' }
    vi.spyOn(api, 'resetAvatar').mockResolvedValue(google)
    const auth = useAuthStore()
    auth.user = { ...ME, avatarUrl: '/api/users/u-me/avatar?v=1' }

    await auth.resetAvatar()

    expect(auth.user).toEqual(google)
  })
})
