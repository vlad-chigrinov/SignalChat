import { watch } from 'vue'

import { getRealtimeService, stopRealtimeService } from '@/realtime'
import { useAuthStore } from '@/stores/auth'
import { usePresenceStore } from '@/stores/presence'
import { useRoomsStore } from '@/stores/rooms'

/**
 * Loads the per-user data (conversation list, presence snapshot) and opens the realtime
 * connection once a session exists; closes it on sign-out.
 */
export function useSessionBootstrap(): void {
  const auth = useAuthStore()
  const rooms = useRoomsStore()
  const presence = usePresenceStore()

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
}
