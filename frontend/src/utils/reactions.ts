import type { Reaction, ReactionCount } from '@/api'
import { reactionOrder } from '@/api/reactionCatalog'

/** The number shown next to an emoji: only when more than one person chose it. */
export function reactionCountLabel(count: number): string {
  return count > 1 ? String(count) : ''
}

/** The emoji the caller holds on a message, or null. */
export function ownEmojiOf(reactions: readonly Reaction[]): string | null {
  return reactions.find((reaction) => reaction.reactedByMe)?.emoji ?? null
}

/**
 * Takes the counts from a live event, which carries no "reacted by me": the caller's own emoji
 * stays marked as long as it is still counted.
 */
export function withCounts(
  current: readonly Reaction[],
  counts: readonly ReactionCount[],
): Reaction[] {
  const own = ownEmojiOf(current)
  return counts
    .filter((count) => count.count > 0)
    .map((count) => ({ emoji: count.emoji, count: count.count, reactedByMe: count.emoji === own }))
    .sort((left, right) => reactionOrder(left.emoji) - reactionOrder(right.emoji))
}

/** Marks the caller's emoji (null: none) without touching the counts. */
export function withOwnEmoji(current: readonly Reaction[], emoji: string | null): Reaction[] {
  return current.map((reaction) => ({ ...reaction, reactedByMe: reaction.emoji === emoji }))
}
