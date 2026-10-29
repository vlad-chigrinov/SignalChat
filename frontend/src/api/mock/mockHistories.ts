import type { Message } from '../types'

import { createMockHistories } from './mockData'

let shared: Map<string, Message[]> | null = null

/**
 * Message histories of the mock backend, one copy per page. The mock REST API reads them and
 * the fake hub appends to them, so a message sent over the hub is in the history afterwards.
 */
export function sharedMockHistories(): Map<string, Message[]> {
  shared ??= createMockHistories()
  return shared
}
