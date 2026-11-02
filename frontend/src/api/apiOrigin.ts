/**
 * Where the API lives. Empty means the SPA and the API share one origin (the Vite proxy in development).
 * A value such as `https://chat-api.vlados.tech` means the API has an origin of its own, as in the deployed
 * environments. Pure functions only: vite.config.ts imports this file, so it must not touch `import.meta`.
 */
const API_ORIGIN_KEY = 'VITE_API_ORIGIN'
const SECURE_WEB_SOCKET_SCHEME = 'wss:'
const WEB_SOCKET_SCHEME = 'ws:'

/** Accepts a bare http(s) origin (no path, query or hash) or nothing; anything else stops the build or the app. */
export function normalizeApiOrigin(raw: string | undefined): string {
  const trimmed = (raw ?? '').trim()
  if (trimmed === '') return ''

  const invalid = () =>
    new Error(`${API_ORIGIN_KEY} must be a bare http(s) origin, got '${trimmed}'.`)
  let url: URL
  try {
    url = new URL(trimmed)
  } catch {
    throw invalid()
  }
  const isHttp = url.protocol === 'https:' || url.protocol === 'http:'
  const hasOnlyOrigin = url.pathname === '/' && url.search === '' && url.hash === ''
  if (!isHttp || !hasOnlyOrigin) throw invalid()
  return url.origin
}

export function joinApiUrl(origin: string, path: string): string {
  return `${origin}${path}`
}

/**
 * Where an avatar is loaded from. The API names an uploaded avatar by a path on itself (`/api/users/{id}/avatar?v=2`);
 * the page may be on another origin, so the path is joined to the API origin. A Google avatar is already absolute.
 * An address that starts with two slashes would point at a host of its choosing: it is dropped.
 */
export function resolveAvatarSrc(origin: string, avatarUrl: string | null): string | null {
  if (avatarUrl === null || !avatarUrl.startsWith('/')) return avatarUrl
  const second = avatarUrl.charAt(1)
  if (second === '/' || second === '\\') return null
  return joinApiUrl(origin, avatarUrl)
}

/** The session cookie goes to another origin only when the API really is on another origin. */
export function credentialsFor(origin: string): RequestCredentials {
  return origin === '' ? 'same-origin' : 'include'
}

/** The origin a WebSocket to this API connects to: `https:` becomes `wss:`, `http:` becomes `ws:`. */
export function webSocketOriginOf(origin: string): string {
  const url = new URL(origin)
  const scheme = url.protocol === 'https:' ? SECURE_WEB_SOCKET_SCHEME : WEB_SOCKET_SCHEME
  return `${scheme}//${url.host}`
}
