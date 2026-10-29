const LOCALE = 'ru-RU'

const timeFormat = new Intl.DateTimeFormat(LOCALE, { hour: '2-digit', minute: '2-digit' })
const dateTimeFormat = new Intl.DateTimeFormat(LOCALE, {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})

function isSameDay(left: Date, right: Date): boolean {
  return left.toDateString() === right.toDateString()
}

/** "14:05" for today, "3 окт., 14:05" otherwise. */
export function formatMessageTime(isoDate: string, now: Date = new Date()): string {
  const date = new Date(isoDate)
  return isSameDay(date, now) ? timeFormat.format(date) : dateTimeFormat.format(date)
}
