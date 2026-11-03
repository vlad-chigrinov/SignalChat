import { safeReturnPath } from '@/utils/safeReturnPath'

import { apiUrl } from './apiConfig'
import type { ChatApi } from './contract'
import { ApiError, requestJson } from './http'
import { DISCOVER_MAX_LIMIT, HISTORY_MAX_LIMIT, USER_SEARCH_MAX_LIMIT, clampLimit } from './limits'
import type {
  ConversationId,
  CreateRoomInput,
  CurrentUser,
  CursorPage,
  DirectConversation,
  IncomingDirectRequest,
  Message,
  MessageId,
  Reaction,
  Room,
  RoomMember,
  UserId,
  UserSummary,
} from './types'

const HTTP_UNAUTHORIZED = 401

export function buildLoginUrl(returnPath: string): string {
  const params = new URLSearchParams({ returnUrl: safeReturnPath(returnPath) })
  return apiUrl(`/auth/login?${params.toString()}`)
}

function roomPath(roomId: ConversationId, action: string): string {
  return `/api/rooms/${encodeURIComponent(roomId)}/${action}`
}

function directPath(conversationId: ConversationId, action: string): string {
  return `/api/direct-conversations/${encodeURIComponent(conversationId)}/${action}`
}

function conversationPath(conversationId: ConversationId): string {
  return `/api/conversations/${encodeURIComponent(conversationId)}/messages`
}

function reactionPath(conversationId: ConversationId, messageId: MessageId): string {
  return `${conversationPath(conversationId)}/${encodeURIComponent(messageId)}/reaction`
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

  listRoomMembers: (roomId) => requestJson<RoomMember[]>(roomPath(roomId, 'members')),

  inviteToRoom: (roomId, userId) =>
    requestJson<void>(roomPath(roomId, 'invitations'), {
      method: 'POST',
      body: jsonBody({ userId }),
    }),

  listDirectConversations: () => requestJson<DirectConversation[]>('/api/direct-conversations'),

  sendDirectRequest: (userId: UserId, text: string) =>
    requestJson<DirectConversation>('/api/direct-conversations', {
      method: 'POST',
      body: jsonBody({ userId, text }),
    }),

  listIncomingRequests: () => requestJson<IncomingDirectRequest[]>('/api/direct-requests/incoming'),

  acceptRequest: (conversationId) =>
    requestJson<void>(directPath(conversationId, 'accept'), { method: 'POST' }),

  declineRequest: (conversationId) =>
    requestJson<void>(directPath(conversationId, 'decline'), { method: 'POST' }),

  searchUsers(query, limit) {
    const params = new URLSearchParams({
      query: query.trim(),
      limit: String(clampLimit(limit, USER_SEARCH_MAX_LIMIT)),
    })
    return requestJson<UserSummary[]>(`/api/users/search?${params}`)
  },

  // Caption first, then the photos in order: the server reads the parts as they arrive and stops at the first bad one.
  sendPhotoMessage(conversationId, caption, photos) {
    const form = new FormData()
    form.append('text', caption)
    for (const photo of photos) form.append('files', photo, photo.name)
    return requestJson<Message>(`${conversationPath(conversationId)}/photos`, {
      method: 'POST',
      body: form,
    })
  },

  getMessages(conversationId, { before, limit }) {
    const params = new URLSearchParams({ limit: String(clampLimit(limit, HISTORY_MAX_LIMIT)) })
    if (before) params.set('before', before)
    return requestJson<CursorPage<Message>>(`${conversationPath(conversationId)}?${params}`)
  },

  async chooseReaction(conversationId, messageId, emoji) {
    const answer = await requestJson<{ messageId: MessageId; reactions: Reaction[] }>(
      reactionPath(conversationId, messageId),
      { method: 'POST', body: jsonBody({ emoji }) },
    )
    return answer.reactions
  },

  getOnlineUserIds: () => requestJson<UserId[]>('/api/presence/online'),

  changeDisplayName: (displayName) =>
    requestJson<CurrentUser>('/api/me/display-name', {
      method: 'PUT',
      body: jsonBody({ displayName }),
    }),

  // The picture goes as the raw body: the server reads its type from the content, not from this header.
  uploadAvatar: (file) =>
    requestJson<CurrentUser>('/api/me/avatar', {
      method: 'PUT',
      body: file,
      headers: { 'Content-Type': 'application/octet-stream' },
    }),

  resetAvatar: () => requestJson<CurrentUser>('/api/me/avatar', { method: 'DELETE' }),
}
