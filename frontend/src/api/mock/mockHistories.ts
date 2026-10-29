import type {
  ConversationId,
  CurrentUser,
  Message,
  MessagePreview,
  UserId,
  UserSummary,
} from '../types'

import { createSeedStore } from './mockData'
import { mockError } from './mockErrors'

export interface MockRoom {
  id: ConversationId
  name: string
  description: string
  ownerId: UserId
  memberIds: Set<UserId>
}

export interface MockDirect {
  id: ConversationId
  /** Exactly two members: the signed-in user and the peer. */
  memberIds: UserId[]
}

/**
 * The whole mock backend. The mock REST API and the fake hub share ONE instance, so a message
 * sent over the hub is in the history, and a joined room is in the room list.
 */
export interface MockStore {
  currentUser: CurrentUser
  /** Other users (the signed-in user is `currentUser`). Never carries an email. */
  directory: UserSummary[]
  rooms: Map<ConversationId, MockRoom>
  directs: Map<ConversationId, MockDirect>
  histories: Map<ConversationId, Message[]>
  nextId: number
}

let shared: MockStore | null = null

/** Created on first use, so a production build that never calls it keeps no demo data. */
export function sharedMockStore(): MockStore {
  shared ??= createSeedStore()
  return shared
}

/** Tests start every case from the seed again. */
export function resetSharedMockStore(): void {
  shared = null
}

export function nextMockId(store: MockStore, prefix: string): ConversationId {
  const id = `${prefix}-${store.nextId}`
  store.nextId += 1
  return id
}

/** Member of a room or of a direct conversation. Anything else is not a member. */
export function isMember(
  store: MockStore,
  conversationId: ConversationId,
  userId: UserId,
): boolean {
  const room = store.rooms.get(conversationId)
  if (room) return room.memberIds.has(userId)
  const direct = store.directs.get(conversationId)
  return direct ? direct.memberIds.includes(userId) : false
}

/**
 * The server answers `not_found` both for a missing conversation and for one the caller is not
 * a member of (C3). Returns the history of the conversation.
 */
export function requireMember(
  store: MockStore,
  conversationId: ConversationId,
  userId: UserId,
): Message[] {
  if (!isMember(store, conversationId, userId)) throw mockError('not_found')
  return historyOf(store, conversationId)
}

export function historyOf(store: MockStore, conversationId: ConversationId): Message[] {
  let history = store.histories.get(conversationId)
  if (!history) {
    history = []
    store.histories.set(conversationId, history)
  }
  return history
}

export function previewOf(store: MockStore, conversationId: ConversationId): MessagePreview | null {
  const last = historyOf(store, conversationId).at(-1)
  return last ? { authorId: last.author.id, text: last.text, sentAt: last.sentAt } : null
}

export function userSummaryOf(store: MockStore, userId: UserId): UserSummary | undefined {
  if (userId === store.currentUser.id) {
    const { id, displayName, avatarUrl } = store.currentUser
    return { id, displayName, avatarUrl }
  }
  return store.directory.find((user) => user.id === userId)
}
