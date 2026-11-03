import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { api, type CurrentUser, type UserSummary } from '@/api'
import { errorMessage } from '@/utils/errorMessage'

import { useDirectRequestsStore } from './directRequests'
import { useMessagesStore } from './messages'
import { usePresenceStore } from './presence'
import { applyProfileChange } from './profileSync'
import { useRoomMembersStore } from './roomMembers'
import { useRoomNoticesStore } from './roomNotices'
import { useRoomsStore } from './rooms'

type AuthStatus = 'unknown' | 'authenticated' | 'anonymous'

/** Session state. Only the user profile is kept, in memory; the token stays in the cookie. */
export const useAuthStore = defineStore('auth', () => {
  const user = ref<CurrentUser | null>(null)
  const status = ref<AuthStatus>('unknown')
  const error = ref<string | null>(null)
  let pendingLoad: Promise<void> | null = null

  const isAuthenticated = computed(() => status.value === 'authenticated')

  async function fetchCurrentUser(): Promise<void> {
    try {
      user.value = await api.getCurrentUser()
      error.value = null
    } catch (caught) {
      user.value = null
      error.value = errorMessage(caught)
    }
    status.value = user.value ? 'authenticated' : 'anonymous'
  }

  /** Resolves the session once; concurrent callers share the same request. */
  function ensureLoaded(): Promise<void> {
    if (status.value !== 'unknown') return Promise.resolve()
    pendingLoad ??= fetchCurrentUser().finally(() => {
      pendingLoad = null
    })
    return pendingLoad
  }

  /** True when the user is signed in without leaving the page (mock mode). */
  async function login(returnPath: string): Promise<boolean> {
    const outcome = await api.beginLogin(returnPath)
    if (outcome === 'redirected') return false
    await fetchCurrentUser()
    return isAuthenticated.value
  }

  /** The signed-in user's own change (a live event from another tab). Anyone else's change is not the session's profile. */
  function applyOwnProfile(profile: UserSummary): void {
    if (user.value?.id !== profile.id) return
    user.value = { ...user.value, displayName: profile.displayName, avatarUrl: profile.avatarUrl }
  }

  /** Takes the profile the server answered with: the header, the profile page and the lists show it at once. */
  function takeUpdatedProfile(profile: CurrentUser): void {
    user.value = profile
    applyProfileChange({
      id: profile.id,
      displayName: profile.displayName,
      avatarUrl: profile.avatarUrl,
    })
  }

  /** Rejects with the API error (`invalid_display_name`); the profile stays as it was. */
  async function changeDisplayName(displayName: string): Promise<void> {
    takeUpdatedProfile(await api.changeDisplayName(displayName))
  }

  async function uploadAvatar(file: Blob): Promise<void> {
    takeUpdatedProfile(await api.uploadAvatar(file))
  }

  async function resetAvatar(): Promise<void> {
    takeUpdatedProfile(await api.resetAvatar())
  }

  async function logout(): Promise<void> {
    try {
      await api.logout()
    } finally {
      // Drop local state even if the request failed: the UI must not keep showing private data.
      user.value = null
      status.value = 'anonymous'
      useRoomsStore().reset()
      useRoomMembersStore().reset()
      useRoomNoticesStore().reset()
      useDirectRequestsStore().reset()
      useMessagesStore().reset()
      usePresenceStore().reset()
    }
  }

  return {
    user,
    status,
    error,
    isAuthenticated,
    ensureLoaded,
    login,
    logout,
    applyOwnProfile,
    changeDisplayName,
    uploadAvatar,
    resetAvatar,
  }
})
