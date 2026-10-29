import type { NavigationGuardWithThis } from 'vue-router'

import { useAuthStore } from '@/stores/auth'

import { ROUTE_NAMES } from './routes'

/**
 * Client-side convenience only: it decides which screen to show. The backend
 * still checks the session and the membership on every request.
 */
export const authGuard: NavigationGuardWithThis<undefined> = async (to) => {
  const auth = useAuthStore()
  await auth.ensureLoaded()

  if (to.name === ROUTE_NAMES.login && auth.isAuthenticated) {
    return { name: ROUTE_NAMES.chatList }
  }
  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: ROUTE_NAMES.login, query: { redirect: to.fullPath } }
  }
  return true
}
