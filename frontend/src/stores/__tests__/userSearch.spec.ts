import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { api, type UserSummary } from '@/api'
import { USER_SEARCH_MAX_LIMIT } from '@/api/limits'

import { useUserSearchStore } from '../userSearch'

const ANNA: UserSummary = { id: 'u-anna', displayName: 'Анна Смирнова', avatarUrl: null }

describe('user search store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('searches with the trimmed query and the server limit', async () => {
    const searchUsers = vi.spyOn(api, 'searchUsers').mockResolvedValue([ANNA])
    const store = useUserSearchStore()

    await store.search('  ан ')

    expect(searchUsers).toHaveBeenCalledWith('ан', USER_SEARCH_MAX_LIMIT)
    expect(store.results).toEqual([ANNA])
  })

  it('clears the results', async () => {
    vi.spyOn(api, 'searchUsers').mockResolvedValue([ANNA])
    const store = useUserSearchStore()
    await store.search('ан')

    store.clear()

    expect(store.results).toEqual([])
  })
})
