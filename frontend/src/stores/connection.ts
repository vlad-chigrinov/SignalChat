import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import type { ConnectionState } from '@/realtime/hubContract'

/** State of the realtime connection, shown to the user as a banner when it is degraded. */
export const useConnectionStore = defineStore('connection', () => {
  const state = ref<ConnectionState>('disconnected')

  const isDegraded = computed(
    () => state.value === 'reconnecting' || state.value === 'disconnected',
  )

  function setState(next: ConnectionState): void {
    state.value = next
  }

  return { state, isDegraded, setState }
})
