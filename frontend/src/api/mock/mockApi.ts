import type { ChatApi } from '../contract'
import { ApiError } from '../http'
import type { Message, MessagePreview } from '../types'
import { mockCurrentUser, mockDirects, mockOnlineUserIds, mockRooms } from './mockData'
import { sharedMockHistories } from './mockHistories'

/** Simulated network latency, so loading states are visible. */
const MOCK_LATENCY_MS = 300
const HTTP_NOT_FOUND = 404

function delay<T>(value: T): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(value), MOCK_LATENCY_MS))
}

/** Cursor = index (exclusive) of the oldest message already delivered. */
function parseCursor(before: string | null, total: number): number {
  if (before === null) return total
  const index = Number.parseInt(before, 10)
  return Number.isNaN(index) ? total : Math.min(Math.max(index, 0), total)
}

export interface MockApiOptions {
  /** Start without a session (e2e: route guard and sign-in scenarios). Default: signed in. */
  startSignedOut?: boolean
}

/**
 * In-memory fake REST backend (read-only: messages are sent through the hub). A production
 * build never calls it, so the bundler drops this module and the demo data with it.
 */
export function createMockApi(options: MockApiOptions = {}): ChatApi {
  const histories = sharedMockHistories()
  // Kept in memory only: a page reload starts signed in again.
  let isSignedIn = !options.startSignedOut

  function historyOf(conversationId: string): Message[] {
    const history = histories.get(conversationId)
    if (!history) throw new ApiError(HTTP_NOT_FOUND, `Unknown conversation ${conversationId}`)
    return history
  }

  function lastPreview(conversationId: string): MessagePreview | null {
    const last = historyOf(conversationId).at(-1)
    return last ? { authorId: last.author.id, text: last.text, sentAt: last.sentAt } : null
  }

  return {
    getCurrentUser: () => delay(isSignedIn ? mockCurrentUser : null),

    beginLogin() {
      isSignedIn = true
      return delay('signed-in' as const)
    },

    logout() {
      isSignedIn = false
      return delay(undefined)
    },

    listRooms: () =>
      delay(mockRooms.map((room) => ({ ...room, lastMessage: lastPreview(room.id) }))),

    listDirectConversations: () =>
      delay(mockDirects.map((direct) => ({ ...direct, lastMessage: lastPreview(direct.id) }))),

    async getMessages(conversationId, { before, limit }) {
      const history = historyOf(conversationId)
      const end = parseCursor(before, history.length)
      const start = Math.max(0, end - limit)
      return delay({
        items: history.slice(start, end),
        nextCursor: start > 0 ? String(start) : null,
      })
    },

    getOnlineUserIds: () => delay([...mockOnlineUserIds]),
  }
}
