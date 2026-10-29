import type { Message } from '@/api'

function compareChronologically(left: Message, right: Message): number {
  if (left.sentAt !== right.sentAt) return left.sentAt < right.sentAt ? -1 : 1
  return left.id < right.id ? -1 : left.id > right.id ? 1 : 0
}

/**
 * Merges new messages into a chronological list, dropping duplicates by id.
 * A message can arrive twice: once from the send response, once from real-time.
 */
export function mergeMessages(
  existing: readonly Message[],
  incoming: readonly Message[],
): Message[] {
  const byId = new Map(existing.map((message) => [message.id, message]))
  for (const message of incoming) byId.set(message.id, message)
  return [...byId.values()].sort(compareChronologically)
}
