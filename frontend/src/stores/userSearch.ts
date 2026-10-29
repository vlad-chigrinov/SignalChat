import { ref } from 'vue'
import { defineStore } from 'pinia'

import { api, type UserSummary } from '@/api'
import { USER_SEARCH_MAX_LIMIT } from '@/api/limits'

/** Other users matching a name. The server never returns the caller or any email. */
export const useUserSearchStore = defineStore('userSearch', () => {
  const results = ref<UserSummary[]>([])

  /** Replaces the results. Errors (such as `query_too_short`) are thrown to the caller. */
  async function search(query: string): Promise<void> {
    results.value = await api.searchUsers(query.trim(), USER_SEARCH_MAX_LIMIT)
  }

  function clear(): void {
    results.value = []
  }

  return { results, search, clear }
})
