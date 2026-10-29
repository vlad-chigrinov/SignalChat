const HTTP_NO_CONTENT = 204

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/**
 * Same-origin JSON request. The session lives in an HttpOnly cookie set by the
 * backend, so this code never reads or stores a token.
 */
export async function requestJson<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  // Lets the backend answer API calls with 401 instead of redirecting them to Google.
  headers.set('X-Requested-With', 'XMLHttpRequest')
  if (init.body !== undefined) headers.set('Content-Type', 'application/json')

  const response = await fetch(path, { ...init, headers, credentials: 'same-origin' })
  if (!response.ok) {
    const method = init.method ?? 'GET'
    throw new ApiError(response.status, `${method} ${path} failed: ${response.status}`)
  }
  if (response.status === HTTP_NO_CONTENT) return undefined as T
  return (await response.json()) as T
}
