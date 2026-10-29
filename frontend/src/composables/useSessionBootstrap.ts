import { watch } from 'vue'

import { useAuthStore } from '@/stores/auth'
import { usePresenceStore } from '@/stores/presence'
import { useRoomsStore } from '@/stores/rooms'

/** Loads the per-user data (conversation list, presence snapshot) once a session exists. */
export function useSessionBootstrap(): void {
  const auth = useAuthStore()
  const rooms = useRoomsStore()
  const presence = usePresenceStore()

  watch(
    () => auth.isAuthenticated,
    (isAuthenticated) => {
      if (!isAuthenticated) return
      void rooms.load()
      void presence.loadSnapshot()
    },
    { immediate: true },
  )
}
