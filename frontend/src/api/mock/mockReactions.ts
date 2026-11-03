import { isReactionEmoji, reactionOrder } from '../reactionCatalog'
import type { ConversationId, Message, MessageId, Reaction, UserId } from '../types'

import { mockError } from './mockErrors'
import { historyOf, isActiveMember, type MockStore } from './mockHistories'

/** Counts per emoji over everyone's choice, with the viewer's own marked. Catalog order. */
export function reactionsOf(store: MockStore, messageId: MessageId, viewerId: UserId): Reaction[] {
  const chosen = store.reactions.get(messageId)
  if (!chosen) return []
  const counts = new Map<string, number>()
  for (const emoji of chosen.values()) counts.set(emoji, (counts.get(emoji) ?? 0) + 1)
  return [...counts.entries()]
    .map(([emoji, count]) => ({ emoji, count, reactedByMe: chosen.get(viewerId) === emoji }))
    .sort((left, right) => reactionOrder(left.emoji) - reactionOrder(right.emoji))
}

/** History items as the viewer sees them: each message with its reactions. */
export function withReactions(
  store: MockStore,
  messages: readonly Message[],
  viewerId: UserId,
): Message[] {
  return messages.map((message) => ({
    ...message,
    reactions: reactionsOf(store, message.id, viewerId),
  }))
}

/**
 * The server's rules for picking a reaction: the caller must be able to write in the conversation
 * (the initiator of a pending request gets `dm_not_accepted`, anyone else `not_found`), the message
 * must be in that conversation, and the emoji must be in the catalog. One reaction per user:
 * another emoji replaces the first, the same one removes it.
 */
export function chooseReaction(
  store: MockStore,
  callerId: UserId,
  conversationId: ConversationId,
  messageId: MessageId,
  emoji: string,
): Reaction[] {
  requireWritable(store, callerId, conversationId)
  if (!historyOf(store, conversationId).some((message) => message.id === messageId)) {
    throw mockError('not_found')
  }
  if (!isReactionEmoji(emoji)) throw mockError('invalid_reaction')

  const chosen = store.reactions.get(messageId) ?? new Map<UserId, string>()
  if (chosen.get(callerId) === emoji) chosen.delete(callerId)
  else chosen.set(callerId, emoji)
  store.reactions.set(messageId, chosen)
  return reactionsOf(store, messageId, callerId)
}

function requireWritable(store: MockStore, userId: UserId, conversationId: ConversationId): void {
  if (isActiveMember(store, conversationId, userId)) return
  const direct = store.directs.get(conversationId)
  if (direct?.initiatorId === userId) throw mockError('dm_not_accepted')
  throw mockError('not_found')
}
