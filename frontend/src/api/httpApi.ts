import { safeReturnPath } from '@/utils/safeReturnPath'

import type { ChatApi } from './contract'
import { ApiError, requestJson } from './http'
import { DISCOVER_MAX_LIMIT, HISTORY_MAX_LIMIT, USER_SEARCH_MAX_LIMIT, clampLimit } from './limits'
import type {
  ConversationId,
  CreateRoomInput,
  CurrentUser,
  CursorPage,
  DirectConversation,
  Message,
  Room,
  UserId,
  UserSummary,
} from './types'

const HTTP_UNAUTHORIZED = 401

export function buildLoginUrl(returnPath: string): string {
  const params = new URLSearchParams({ returnUrl: safeReturnPath(returnPath) })
  return `/auth/login?${params.toString()}`
}

function roomPath(roomId: ConversationId, action: string): string {
  return `/api/rooms/${encodeURIComponent(roomId)}/${action}`
}

function conversationPath(conversationId: ConversationId): string {
  return `/api/conversations/${encodeURIComponent(conversationId)}/messages`
}

function jsonBody(value: unknown): string {
  return JSON.stringify(value)
}

export const httpApi: ChatApi = {
  async getCurrentUser() {
    try {
      return await requestJson<CurrentUser>('/auth/me')
    } catch (error) {
      if (error instanceof ApiError && error.status === HTTP_UNAUTHORIZED) return null
      throw error
    }
  },

  async beginLogin(returnPath) {
    // Full navigation: the backend builds the Google redirect URI and sets the cookie.
    window.location.assign(buildLoginUrl(returnPath))
    return 'redirected'
  },

  logout: () => requestJson<void>('/auth/logout', { method: 'POST' }),

  listRooms: () => requestJson<Room[]>('/api/rooms'),

  discoverRooms(query, limit) {
    const params = new URLSearchParams({
      query: query.trim(),
      limit: String(clampLimit(limit, DISCOVER_MAX_LIMIT)),
    })
    return requestJson<Room[]>(`/api/rooms/discover?${params}`)
  },

  createRoom: (input: CreateRoomInput) =>
    requestJson<Room>('/api/rooms', { method: 'POST', body: jsonBody(input) }),

  joinRoom: (roomId) => requestJson<void>(roomPath(roomId, 'join'), { method: 'POST' }),

  leaveRoom: (roomId) => requestJson<void>(roomPath(roomId, 'leave'), { method: 'POST' }),

  listDirectConversations: () => requestJson<DirectConversation[]>('/api/direct-conversations'),

  openDirectConversation: (userId: UserId) =>
    requestJson<DirectConversation>('/api/direct-conversations', {
      method: 'POST',
      body: jsonBody({ userId }),
    }),

  searchUsers(query, limit) {
    const params = new URLSearchParams({
      query: query.trim(),
      limit: String(clampLimit(limit, USER_SEARCH_MAX_LIMIT)),
    })
    return requestJson<UserSummary[]>(`/api/users/search?${params}`)
  },

  getMessages(conversationId, { before, limit }) {
    const params = new URLSearchParams({ limit: String(clampLimit(limit, HISTORY_MAX_LIMIT)) })
    if (before) params.set('before', before)
    return requestJson<CursorPage<Message>>(`${conversationPath(conversationId)}?${params}`)
  },

  getOnlineUserIds: () => requestJson<UserId[]>('/api/presence/online'),
}
