import type { ConversationId, RoomMember, UserId } from '../types'

import { mockError } from './mockErrors'
import { type MockStore, announceRoomMemberJoined, userSummaryOf } from './mockHistories'

/** Accepted chats only: a waiting or declined request is not a chat (CONTRACT.md). */
function haveAcceptedChat(store: MockStore, userId: UserId, otherId: UserId): boolean {
  if (userId === otherId) return false
  return [...store.directs.values()].some(
    (direct) =>
      direct.status === 'accepted' &&
      direct.memberIds.includes(userId) &&
      direct.memberIds.includes(otherId),
  )
}

/** Members only: a non-member and a missing room get the same `not_found`. The owner comes first. */
export function listRoomMembers(
  store: MockStore,
  callerId: UserId,
  roomId: ConversationId,
): RoomMember[] {
  const room = store.rooms.get(roomId)
  if (!room || !room.memberIds.has(callerId)) throw mockError('not_found')

  return [...room.memberIds]
    .map((memberId) => ({ user: userSummaryOf(store, memberId), memberId }))
    .flatMap(({ user, memberId }) =>
      user
        ? [
            {
              user: { ...user },
              role: memberId === room.ownerId ? ('owner' as const) : ('member' as const),
            },
          ]
        : [],
    )
    .sort((left, right) => Number(right.role === 'owner') - Number(left.role === 'owner'))
}

/**
 * A member adds a chat partner at once. Every refusal that would reveal who exists or who chats with whom is
 * `not_found`: the room is not the caller's, the user is unknown, or there is no accepted chat with them.
 */
export function inviteToRoom(
  store: MockStore,
  callerId: UserId,
  roomId: ConversationId,
  inviteeId: UserId,
): void {
  const room = store.rooms.get(roomId)
  if (!room || !room.memberIds.has(callerId)) throw mockError('not_found')
  if (!userSummaryOf(store, inviteeId) || !haveAcceptedChat(store, callerId, inviteeId)) {
    throw mockError('not_found')
  }
  if (room.memberIds.has(inviteeId)) throw mockError('already_member')

  room.memberIds.add(inviteeId)
  announceRoomMemberJoined(store, roomId, inviteeId)
}
