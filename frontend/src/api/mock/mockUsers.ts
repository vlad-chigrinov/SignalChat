import type { UserSummary } from '../types'

import { mockError } from './mockErrors'
import type { MockStore } from './mockHistories'

const USER_SEARCH_MIN_QUERY_LENGTH = 2

/** Other users only (the caller is never in the result), matched by display name. No email. */
export function searchUsers(
  store: MockStore,
  callerId: string,
  query: string,
  limit: number,
): UserSummary[] {
  const needle = query.trim().toLowerCase()
  if (needle.length < USER_SEARCH_MIN_QUERY_LENGTH) throw mockError('query_too_short')
  return store.directory
    .filter((user) => user.id !== callerId)
    .filter((user) => user.displayName.toLowerCase().includes(needle))
    .slice(0, limit)
    .map((user) => ({ id: user.id, displayName: user.displayName, avatarUrl: user.avatarUrl }))
}
