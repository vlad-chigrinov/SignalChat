import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { usePresenceStore } from '../presence'

describe('presence store', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('applies online and offline changes', () => {
    const store = usePresenceStore()
    store.applyPresenceChanged({ userId: 'u1', isOnline: true })
    expect(store.isOnline('u1')).toBe(true)

    store.applyPresenceChanged({ userId: 'u1', isOnline: false })
    expect(store.isOnline('u1')).toBe(false)
  })
})
