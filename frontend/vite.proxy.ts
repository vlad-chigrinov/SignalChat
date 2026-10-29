import type { ProxyOptions } from 'vite'

/** Backend paths forwarded by the Vite dev server. */
const HTTP_PREFIXES = ['/api', '/auth'] as const
const HUBS_PREFIX = '/hubs'

/** Default ASP.NET Core HTTPS dev endpoint; override with BACKEND_URL in frontend/.env. */
export const DEFAULT_BACKEND_URL = 'https://localhost:5001'

/**
 * Builds the dev proxy table. `secure: false` accepts the self-signed
 * `dotnet dev-certs` certificate; it only applies to local development.
 */
export function createBackendProxy(backendUrl: string): Record<string, ProxyOptions> {
  const base: ProxyOptions = { target: backendUrl, changeOrigin: true, secure: false }

  const proxy: Record<string, ProxyOptions> = {}
  for (const prefix of HTTP_PREFIXES) {
    proxy[prefix] = { ...base }
  }
  // SignalR negotiates over HTTP, then upgrades to WebSocket (wss://).
  proxy[HUBS_PREFIX] = { ...base, ws: true }
  return proxy
}
