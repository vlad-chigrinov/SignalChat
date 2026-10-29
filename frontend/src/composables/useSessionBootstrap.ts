import { watch } from 'vue'
import { useRouter } from 'vue-router'

import { getRealtimeService, stopRealtimeService } from '@/realtime'
import { ROUTE_NAMES } from '@/router/routes'
import { useAuthStore } from '@/stores/auth'
import { useConnectionStore } from '@/stores/connection'
import { usePresenceStore } from '@/stores/presence'
import { useRoomsStore } from '@/stores/rooms'

/**
 * Loads the per-user data (conversation list, presence snapshot) and opens the realtime
 * connection once a session exists; closes it on sign-out. When the server ends the session,
 * the user is signed out locally and sent to the sign-in page.
 */
export function useSessionBootstrap(): void {
  const auth = useAuthStore()
  const rooms = useRoomsStore()
  const presence = usePresenceStore()
  const connection = useConnectionStore()
  const router = useRouter()

  watch(
    () => auth.isAuthenticated,
    (isAuthenticated) => {
      if (!isAuthenticated) {
        void stopRealtimeService()
        return
      }
      void rooms.load()
      void presence.loadSnapshot()
      // A failed start leaves the connection state at "disconnected": the banner shows it.
      void getRealtimeService()
        .connect()
        .catch(() => undefined)
    },
    { immediate: true },
  )

  watch(
    () => connection.sessionEnded,
    async (ended) => {
      if (!ended) return
      connection.clearSessionEnded()
      // The session is already gone on the server: a failed logout call is expected.
      await auth.logout().catch(() => undefined)
      await router.push({ name: ROUTE_NAMES.login })
    },
  )
}
