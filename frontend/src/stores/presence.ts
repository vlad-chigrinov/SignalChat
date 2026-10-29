import { ref } from 'vue'
import { defineStore } from 'pinia'

import { api, type PresenceChange, type UserId } from '@/api'

/** Who is online. Snapshot over REST, then live changes from SignalR (#7). */
export const usePresenceStore = defineStore('presence', () => {
  const onlineUserIds = ref(new Set<UserId>())

  function isOnline(userId: UserId): boolean {
    return onlineUserIds.value.has(userId)
  }

  async function loadSnapshot(): Promise<void> {
    try {
      onlineUserIds.value = new Set(await api.getOnlineUserIds())
    } catch {
      // Presence is decorative: a failure leaves everyone shown as offline.
    }
  }

  /** Real-time entry point: a user went online or offline. */
  function applyPresenceChanged({ userId, isOnline: online }: PresenceChange): void {
    if (online) onlineUserIds.value.add(userId)
    else onlineUserIds.value.delete(userId)
  }

  function reset(): void {
    onlineUserIds.value = new Set()
  }

  return { onlineUserIds, isOnline, loadSnapshot, applyPresenceChanged, reset }
})
