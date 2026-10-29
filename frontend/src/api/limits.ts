/** Upper bounds the client applies before it calls the server, so a request never exceeds them. */
export const DISCOVER_MAX_LIMIT = 50
export const USER_SEARCH_MAX_LIMIT = 20
export const HISTORY_DEFAULT_LIMIT = 30
export const HISTORY_MAX_LIMIT = 100

/** Keeps a requested page size within 1..max. Non-integers are rounded down. */
export function clampLimit(requested: number, max: number): number {
  if (!Number.isFinite(requested)) return max
  return Math.min(Math.max(Math.trunc(requested), 1), max)
}
