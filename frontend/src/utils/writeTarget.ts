import type { ConversationId, DirectConversation, IncomingDirectRequest, UserId } from '@/api'

/** What "write to this person" does, given the chats and requests the user already has. */
export type WriteTarget =
  /** An accepted chat exists: open it. */
  | { kind: 'open'; conversationId: ConversationId }
  /** The user already sent a request that has no answer: do not send a second one. */
  | { kind: 'pending' }
  /** The other person wrote first: the user answers that request. */
  | { kind: 'incoming' }
  /** Nothing yet: the first message is a request. */
  | { kind: 'request' }

export function resolveWriteTarget(
  userId: UserId,
  directs: readonly DirectConversation[],
  incoming: readonly IncomingDirectRequest[],
): WriteTarget {
  const direct = directs.find((item) => item.peer.id === userId)
  if (direct?.status === 'accepted') return { kind: 'open', conversationId: direct.id }
  if (direct) return { kind: 'pending' }
  if (incoming.some((request) => request.from.id === userId)) return { kind: 'incoming' }
  return { kind: 'request' }
}
