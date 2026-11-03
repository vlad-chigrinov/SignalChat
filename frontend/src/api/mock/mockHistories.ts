import type {
  ConversationId,
  CurrentUser,
  Message,
  MessagePreview,
  MessageId,
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

/** `declined` is kept for the initiator's view only: they see it as `pending` (CONTRACT.md). */
export type MockDirectStatus = 'accepted' | 'pending' | 'declined'

export interface MockDirect {
  id: ConversationId
  /** Exactly two members: the user who wrote the first message and the peer. */
  memberIds: UserId[]
  /** Who wrote the request. Equals the other member once the chat is accepted, too. */
  initiatorId: UserId
  status: MockDirectStatus
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
  /** What each user chose per message: one emoji per user. The messages themselves carry no reactions here. */
  reactions: Map<MessageId, Map<UserId, string>>
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

/**
 * Active member: a room member, or a member of an accepted direct conversation. This is what
 * the hub needs to join and to send (a pending request is not a chat yet).
 */
export function isActiveMember(
  store: MockStore,
  conversationId: ConversationId,
  userId: UserId,
): boolean {
  const room = store.rooms.get(conversationId)
  if (room) return room.memberIds.has(userId)
  const direct = store.directs.get(conversationId)
  return direct !== undefined && direct.status === 'accepted' && direct.memberIds.includes(userId)
}

/** Who may read the history: an active member, or the initiator of a request (its own request). */
function canReadHistory(store: MockStore, conversationId: ConversationId, userId: UserId): boolean {
  if (isActiveMember(store, conversationId, userId)) return true
  const direct = store.directs.get(conversationId)
  return direct !== undefined && direct.initiatorId === userId
}

/**
 * The server answers `not_found` both for a missing conversation and for one the caller may not
 * read (C3). Returns the history of the conversation.
 */
export function requireMember(
  store: MockStore,
  conversationId: ConversationId,
  userId: UserId,
): Message[] {
  if (!canReadHistory(store, conversationId, userId)) throw mockError('not_found')
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
