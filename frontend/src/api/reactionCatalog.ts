/**
 * The reactions a message accepts, in display order. The server holds the same list
 * (`ReactionCatalog` in the backend domain) and refuses anything else: change both together.
 */
export const REACTION_EMOJIS = ['👍', '❤️', '😂', '😮', '😢', '🎉'] as const

export type ReactionEmoji = (typeof REACTION_EMOJIS)[number]

export function isReactionEmoji(value: string): value is ReactionEmoji {
  return (REACTION_EMOJIS as readonly string[]).includes(value)
}

/** Position in the display order; an unknown emoji sorts last. */
export function reactionOrder(emoji: string): number {
  const index = (REACTION_EMOJIS as readonly string[]).indexOf(emoji)
  return index === -1 ? REACTION_EMOJIS.length : index
}
