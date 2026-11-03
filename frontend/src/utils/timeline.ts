import type { Message, UserSummary } from '@/api'

/**
 * A line the room shows between messages without being one: it is not stored on the server, has no place in the
 * history pages, and disappears with the page. Only "member joined" exists so far.
 */
export interface RoomNotice {
  id: string
  kind: 'member-joined'
  user: UserSummary
  /** When it happened, ISO 8601: places the notice among the messages. */
  at: string
}

export type TimelineEntry =
  | { key: string; type: 'message'; message: Message }
  | { key: string; type: 'notice'; notice: RoomNotice }

/**
 * Messages and notices in one list, by time. The messages keep their order, and a notice goes before the first
 * message that is newer than it (notices of one moment keep the order they came in). Pure: the history that the
 * pagination reads is not touched, so a notice never shifts a page or a cursor.
 */
export function buildTimeline(
  messages: readonly Message[],
  notices: readonly RoomNotice[],
): TimelineEntry[] {
  const entries: TimelineEntry[] = []
  let nextNotice = 0

  const takeNoticesBefore = (limit: number): void => {
    while (nextNotice < notices.length) {
      const notice = notices[nextNotice]
      if (notice === undefined || Date.parse(notice.at) >= limit) return
      entries.push({ key: `notice:${notice.id}`, type: 'notice', notice })
      nextNotice += 1
    }
  }

  for (const message of messages) {
    takeNoticesBefore(Date.parse(message.sentAt))
    entries.push({ key: `message:${message.id}`, type: 'message', message })
  }
  takeNoticesBefore(Number.POSITIVE_INFINITY)
  return entries
}
