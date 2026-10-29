import { MAX_MESSAGE_LENGTH } from '../../utils/messageText'
import type {
  ConversationId,
  DirectConversation,
  DirectStatus,
  IncomingDirectRequest,
  UserId,
} from '../types'

import { mockError } from './mockErrors'
import {
  type MockDirect,
  type MockStore,
  historyOf,
  nextMockId,
  previewOf,
  userSummaryOf,
} from './mockHistories'

/** Most requests one user may have waiting for an answer (CONTRACT.md). */
export const OUTGOING_REQUEST_LIMIT = 20

function toDirect(store: MockStore, direct: MockDirect, userId: UserId): DirectConversation {
  const peerId = direct.memberIds.find((memberId) => memberId !== userId)
  const peer = peerId === undefined ? undefined : userSummaryOf(store, peerId)
  if (!peer) throw mockError('not_found')
  // A declined request is shown to its initiator as pending: the decline is never revealed.
  const status: DirectStatus = direct.status === 'accepted' ? 'accepted' : 'pending'
  return { kind: 'direct', id: direct.id, peer, status, lastMessage: previewOf(store, direct.id) }
}

function findPairDirect(store: MockStore, userId: UserId, peerId: UserId): MockDirect | undefined {
  return [...store.directs.values()].find(
    (direct) => direct.memberIds.includes(userId) && direct.memberIds.includes(peerId),
  )
}

function outgoingPendingCount(store: MockStore, userId: UserId): number {
  return [...store.directs.values()].filter(
    (direct) => direct.status === 'pending' && direct.initiatorId === userId,
  ).length
}

/** Accepted chats of the caller, and the caller's own requests (shown as pending). */
export function listMyDirects(store: MockStore, userId: UserId): DirectConversation[] {
  return [...store.directs.values()]
    .filter(
      (direct) =>
        direct.memberIds.includes(userId) &&
        (direct.status === 'accepted' || direct.initiatorId === userId),
    )
    .map((direct) => toDirect(store, direct, userId))
}

function requireRequestText(rawText: string): string {
  const text = rawText.trim()
  if (text.length === 0 || text.length > MAX_MESSAGE_LENGTH) throw mockError('invalid_message')
  return text
}

/**
 * Writes the first message to `peerId`. An accepted chat answers as it is (the text is ignored).
 * Otherwise the message is a request: a new one, or a declined one reopened by the user who
 * declined it. Repeats and requests in the other direction are refused with the codes of
 * CONTRACT.md.
 */
export function sendDirectRequest(
  store: MockStore,
  userId: UserId,
  peerId: UserId,
  rawText: string,
): DirectConversation {
  if (peerId === userId) throw mockError('self_conversation')
  if (!userSummaryOf(store, peerId)) throw mockError('not_found')
  const text = requireRequestText(rawText)

  const existing = findPairDirect(store, userId, peerId)
  if (existing?.status === 'accepted') return toDirect(store, existing, userId)
  if (existing?.initiatorId === userId) throw mockError('dm_request_pending')
  if (existing?.status === 'pending') throw mockError('dm_incoming_request_exists')
  if (outgoingPendingCount(store, userId) >= OUTGOING_REQUEST_LIMIT) {
    throw mockError('dm_request_limit_reached')
  }

  const author = userSummaryOf(store, userId)
  if (!author) throw mockError('not_found')
  const direct: MockDirect = existing ?? {
    id: nextMockId(store, 'dm'),
    memberIds: [userId, peerId],
    initiatorId: userId,
    status: 'pending',
  }
  direct.initiatorId = userId
  direct.status = 'pending'
  store.directs.set(direct.id, direct)
  historyOf(store, direct.id).push({
    id: nextMockId(store, 'msg'),
    conversationId: direct.id,
    author,
    text,
    sentAt: new Date().toISOString(),
  })
  return toDirect(store, direct, userId)
}

/** Pending requests the caller has to answer. The newest request text is the one shown. */
export function listIncomingRequests(store: MockStore, userId: UserId): IncomingDirectRequest[] {
  return [...store.directs.values()]
    .filter(
      (direct) =>
        direct.status === 'pending' &&
        direct.memberIds.includes(userId) &&
        direct.initiatorId !== userId,
    )
    .map((direct) => {
      const latest = historyOf(store, direct.id).at(-1)
      const from = userSummaryOf(store, direct.initiatorId)
      if (!latest || !from) throw mockError('not_found')
      return { id: direct.id, from, text: latest.text, createdAt: latest.sentAt }
    })
}

/** The request the caller may answer: pending, and written by the other member. */
function requireIncomingRequest(
  store: MockStore,
  userId: UserId,
  conversationId: ConversationId,
): MockDirect {
  const direct = store.directs.get(conversationId)
  const isAnswerable =
    direct !== undefined &&
    direct.status === 'pending' &&
    direct.memberIds.includes(userId) &&
    direct.initiatorId !== userId
  if (!direct || !isAnswerable) throw mockError('not_found')
  return direct
}

export function acceptRequest(
  store: MockStore,
  userId: UserId,
  conversationId: ConversationId,
): void {
  requireIncomingRequest(store, userId, conversationId).status = 'accepted'
}

/** Silent: the initiator is not told, and sees the request as pending still. */
export function declineRequest(
  store: MockStore,
  userId: UserId,
  conversationId: ConversationId,
): void {
  requireIncomingRequest(store, userId, conversationId).status = 'declined'
}
