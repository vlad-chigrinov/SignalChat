import { USE_MOCK_API } from '@/api'
import { useAuthStore } from '@/stores/auth'

import type { ChatHub } from './chatHub'
import { FakeChatHub } from './fakeChatHub'
import { createRealtimeService, type RealtimeService } from './realtimeService'
import { SignalrChatHub } from './signalrChatHub'

/** The one place that decides which hub implementation the app uses. */
export function createChatHub(): ChatHub {
  if (USE_MOCK_API) return new FakeChatHub({ currentUser: () => useAuthStore().user })
  return new SignalrChatHub()
}

let service: RealtimeService | null = null

/** One realtime connection per browser tab, created on first use. Needs an active Pinia. */
export function getRealtimeService(): RealtimeService {
  service ??= createRealtimeService(createChatHub())
  return service
}

/** Stops the connection if it was ever started. Does nothing for anonymous visitors. */
export async function stopRealtimeService(): Promise<void> {
  if (service) await service.disconnect()
}
