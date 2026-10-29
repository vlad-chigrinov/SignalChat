/** The server answers `query_too_short` below this length (after trim). */
export const USER_SEARCH_MIN_QUERY_LENGTH = 2

const SHORT_QUERY_TEXT = 'Введите минимум 2 символа.'

/** Text to show under the search field when the query is too short; null when it may be sent. */
export function userQueryHint(query: string): string | null {
  return query.trim().length < USER_SEARCH_MIN_QUERY_LENGTH ? SHORT_QUERY_TEXT : null
}
