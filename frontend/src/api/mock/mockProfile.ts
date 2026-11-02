import { displayNameError } from '@/utils/profileForm'

import { AVATAR_ALLOWED_TYPES, AVATAR_MAX_UPLOAD_BYTES } from '../limits'
import type { CurrentUser } from '../types'

import { mockError } from './mockErrors'
import type { MockStore } from './mockHistories'

/** The Google avatar of the demo user (the demo user has none, so "back to Google" shows the initials). */
const MOCK_GOOGLE_AVATAR_URL: string | null = null

/** Same rules as the server: a name that is empty after trimming, too long or with control characters is refused. */
export function changeDisplayName(store: MockStore, displayName: string): CurrentUser {
  if (displayNameError(displayName) !== null) throw mockError('invalid_display_name')
  return updateProfile(store, { displayName: displayName.trim() })
}

/**
 * The mock cannot decode a picture, so it trusts the type the browser reports (the server never does) and keeps
 * the file as an object URL. The size limit is the server's.
 */
export function uploadAvatar(store: MockStore, file: Blob): CurrentUser {
  if (file.size > AVATAR_MAX_UPLOAD_BYTES) throw mockError('avatar_too_large')
  if (!AVATAR_ALLOWED_TYPES.includes(file.type)) throw mockError('invalid_avatar')
  releaseUploadedAvatar(store)
  return updateProfile(store, { avatarUrl: URL.createObjectURL(file) })
}

export function resetAvatar(store: MockStore): CurrentUser {
  releaseUploadedAvatar(store)
  return updateProfile(store, { avatarUrl: MOCK_GOOGLE_AVATAR_URL })
}

function releaseUploadedAvatar(store: MockStore): void {
  const current = store.currentUser.avatarUrl
  if (current?.startsWith('blob:')) URL.revokeObjectURL(current)
}

/** Updates the signed-in user and the messages they wrote, so every view of the mock shows the change. */
function updateProfile(
  store: MockStore,
  change: Partial<Pick<CurrentUser, 'displayName' | 'avatarUrl'>>,
): CurrentUser {
  store.currentUser = { ...store.currentUser, ...change }
  const { id, displayName, avatarUrl } = store.currentUser
  for (const history of store.histories.values()) {
    for (const message of history) {
      if (message.author.id === id) message.author = { id, displayName, avatarUrl }
    }
  }
  return store.currentUser
}
