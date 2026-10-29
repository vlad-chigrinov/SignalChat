import { safeReturnPath } from '@/utils/safeReturnPath'

import type { ChatApi } from './contract'
import { ApiError, requestJson } from './http'
import type { CurrentUser, CursorPage, DirectConversation, Message, Room, UserId } from './types'

const HTTP_UNAUTHORIZED = 401

export function buildLoginUrl(returnPath: string): string {
  const params = new URLSearchParams({ returnUrl: safeReturnPath(returnPath) })
  return `/auth/login?${params.toString()}`
}

function conversationPath(conversationId: string): string {
  return `/api/conversations/${encodeURIComponent(conversationId)}/messages`
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

  listDirectConversations: () => requestJson<DirectConversation[]>('/api/direct-conversations'),

  getMessages(conversationId, { before, limit }) {
    const params = new URLSearchParams({ limit: String(limit) })
    if (before) params.set('before', before)
    return requestJson<CursorPage<Message>>(`${conversationPath(conversationId)}?${params}`)
  },

  getOnlineUserIds: () => requestJson<UserId[]>('/api/presence/online'),
}
