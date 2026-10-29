import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import type { ConnectionState } from '@/realtime/hubContract'

/**
 * State of the realtime connection, shown as a banner when it is degraded. `sessionEnded`
 * is set when the server closed the connection and the session is no longer valid.
 */
export const useConnectionStore = defineStore('connection', () => {
  const state = ref<ConnectionState>('disconnected')
  const sessionEnded = ref(false)

  const isDegraded = computed(
    () => state.value === 'reconnecting' || state.value === 'disconnected',
  )

  function setState(next: ConnectionState): void {
    state.value = next
  }

  function markSessionEnded(): void {
    sessionEnded.value = true
  }

  function clearSessionEnded(): void {
    sessionEnded.value = false
  }

  return { state, isDegraded, sessionEnded, setState, markSessionEnded, clearSessionEnded }
})
