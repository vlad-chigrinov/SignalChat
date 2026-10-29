import type { DirectConversation, UserId } from '../types'

import { mockError } from './mockErrors'
import {
  type MockDirect,
  type MockStore,
  nextMockId,
  previewOf,
  userSummaryOf,
} from './mockHistories'

function toDirect(store: MockStore, direct: MockDirect, userId: UserId): DirectConversation {
  const peerId = direct.memberIds.find((memberId) => memberId !== userId)
  const peer = peerId === undefined ? undefined : userSummaryOf(store, peerId)
  if (!peer) throw mockError('not_found')
  return { kind: 'direct', id: direct.id, peer, lastMessage: previewOf(store, direct.id) }
}

export function listMyDirects(store: MockStore, userId: UserId): DirectConversation[] {
  return [...store.directs.values()]
    .filter((direct) => direct.memberIds.includes(userId))
    .map((direct) => toDirect(store, direct, userId))
}

/**
 * Opens the direct conversation with `peerId`. Answers the existing one when there is already
 * a conversation between the two users, so repeated calls do not create duplicates.
 */
export function openDirectConversation(
  store: MockStore,
  userId: UserId,
  peerId: UserId,
): DirectConversation {
  if (peerId === userId) throw mockError('self_conversation')
  if (!store.directory.some((user) => user.id === peerId)) throw mockError('not_found')

  const existing = [...store.directs.values()].find(
    (direct) => direct.memberIds.includes(userId) && direct.memberIds.includes(peerId),
  )
  if (existing) return toDirect(store, existing, userId)

  const direct: MockDirect = {
    id: nextMockId(store, 'dm'),
    memberIds: [userId, peerId],
  }
  store.directs.set(direct.id, direct)
  return toDirect(store, direct, userId)
}
