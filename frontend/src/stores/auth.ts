import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { api, type CurrentUser } from '@/api'
import { errorMessage } from '@/utils/errorMessage'

import { useDirectRequestsStore } from './directRequests'
import { useMessagesStore } from './messages'
import { usePresenceStore } from './presence'
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

  async function logout(): Promise<void> {
    try {
      await api.logout()
    } finally {
      // Drop local state even if the request failed: the UI must not keep showing private data.
      user.value = null
      status.value = 'anonymous'
      useRoomsStore().reset()
      useDirectRequestsStore().reset()
      useMessagesStore().reset()
      usePresenceStore().reset()
    }
  }

  return { user, status, error, isAuthenticated, ensureLoaded, login, logout }
})
