import { credentialsFor, joinApiUrl, normalizeApiOrigin } from './apiOrigin'

/** Set at build time (`VITE_API_ORIGIN`); empty in development, where the Vite proxy makes the API same-origin. */
export const API_ORIGIN = normalizeApiOrigin(import.meta.env.VITE_API_ORIGIN)

export const FETCH_CREDENTIALS = credentialsFor(API_ORIGIN)

/** Absolute URL of an API path when the API has its own origin, the path itself otherwise. */
export function apiUrl(path: string): string {
  return joinApiUrl(API_ORIGIN, path)
}
