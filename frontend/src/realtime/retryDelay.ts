import { RECONNECT_DELAYS_MS } from './hubContract'

/** Delay before retry number `attempt` (0-based). The last delay repeats once the list runs out. */
export function retryDelayMs(attempt: number): number {
  const lastIndex = RECONNECT_DELAYS_MS.length - 1
  return RECONNECT_DELAYS_MS[Math.min(attempt, lastIndex)] ?? 0
}
