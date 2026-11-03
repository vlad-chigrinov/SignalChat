import type { ChatApi } from '../contract'
import { DISCOVER_MAX_LIMIT, HISTORY_MAX_LIMIT, USER_SEARCH_MAX_LIMIT, clampLimit } from '../limits'
import type { ConversationId, CursorPage, Message, UserId } from '../types'

import { mockError } from './mockErrors'
import { requireMember, sharedMockStore } from './mockHistories'
import { chooseReaction, withReactions } from './mockReactions'
import {
  acceptRequest,
  declineRequest,
  listIncomingRequests,
  listMyDirects,
  sendDirectRequest,
} from './mockDirects'
import { createRoom, discoverRooms, joinRoom, leaveRoom, listMyRooms } from './mockRooms'
import { changeDisplayName, resetAvatar, uploadAvatar } from './mockProfile'
import { searchUsers } from './mockUsers'
import { mockOnlineUserIds } from './mockData'

/** Simulated network latency, so loading states are visible. */
const MOCK_LATENCY_MS = 300
const CURSOR_PATTERN = /^\d+$/

function delay<T>(value: T): Promise<T> {
  return new Promise((resolve) => setTimeout(() => resolve(value), MOCK_LATENCY_MS))
}

/** Cursor = index (exclusive) of the oldest message already delivered. */
function parseCursor(before: string | null, total: number): number {
  if (before === null) return total
  const index = CURSOR_PATTERN.test(before) ? Number(before) : Number.NaN
  if (!Number.isInteger(index) || index > total) throw mockError('invalid_cursor')
  return index
}

function checkLimit(limit: number): void {
  if (!Number.isInteger(limit) || limit < 1 || limit > HISTORY_MAX_LIMIT) {
    throw mockError('invalid_limit')
  }
}

export interface MockApiOptions {
  /** Start without a session (e2e: route guard and sign-in scenarios). Default: signed in. */
  startSignedOut?: boolean
}

/**
 * In-memory fake REST backend. It follows the same rules and error codes as the server
 * (CONTRACT.md) and shares its data with the fake hub. A production build never calls it, so
 * the bundler drops this module and the demo data with it.
 */
export function createMockApi(options: MockApiOptions = {}): ChatApi {
  // Kept in memory only: a page reload starts signed in again.
  let isSignedIn = !options.startSignedOut

  /** The signed-in user the rules act for. Read per call, so a reset store is picked up. */
  function callerId(): UserId {
    return sharedMockStore().currentUser.id
  }

  return {
    getCurrentUser: () => delay(isSignedIn ? sharedMockStore().currentUser : null),

    beginLogin() {
      isSignedIn = true
      return delay('signed-in' as const)
    },

    logout() {
      isSignedIn = false
      return delay(undefined)
    },

    listRooms: () => delay(listMyRooms(sharedMockStore(), callerId())),

    discoverRooms: (query, limit) =>
      delay(
        discoverRooms(sharedMockStore(), callerId(), query, clampLimit(limit, DISCOVER_MAX_LIMIT)),
      ),

    createRoom: (input) => delay(createRoom(sharedMockStore(), callerId(), input)),

    joinRoom: (roomId) => delay(joinRoom(sharedMockStore(), callerId(), roomId)),

    leaveRoom: (roomId) => delay(leaveRoom(sharedMockStore(), callerId(), roomId)),

    listDirectConversations: () => delay(listMyDirects(sharedMockStore(), callerId())),

    sendDirectRequest: (peerId, text) =>
      delay(sendDirectRequest(sharedMockStore(), callerId(), peerId, text)),

    listIncomingRequests: () => delay(listIncomingRequests(sharedMockStore(), callerId())),

    acceptRequest: (conversationId) =>
      delay(acceptRequest(sharedMockStore(), callerId(), conversationId)),

    declineRequest: (conversationId) =>
      delay(declineRequest(sharedMockStore(), callerId(), conversationId)),

    searchUsers: (query, limit) =>
      delay(
        searchUsers(sharedMockStore(), callerId(), query, clampLimit(limit, USER_SEARCH_MAX_LIMIT)),
      ),

    async getMessages(conversationId: ConversationId, { before, limit }) {
      const store = sharedMockStore()
      const history: Message[] = requireMember(store, conversationId, callerId())
      checkLimit(limit)
      const end = parseCursor(before, history.length)
      const start = Math.max(0, end - limit)
      const page: CursorPage<Message> = {
        items: withReactions(store, history.slice(start, end), callerId()),
        nextCursor: start > 0 ? String(start) : null,
      }
      return delay(page)
    },

    chooseReaction: (conversationId, messageId, emoji) =>
      delay(chooseReaction(sharedMockStore(), callerId(), conversationId, messageId, emoji)),

    getOnlineUserIds: () => delay([...mockOnlineUserIds]),

    changeDisplayName: (displayName) => delay(changeDisplayName(sharedMockStore(), displayName)),

    uploadAvatar: (file) => delay(uploadAvatar(sharedMockStore(), file)),

    resetAvatar: () => delay(resetAvatar(sharedMockStore())),
  }
}
