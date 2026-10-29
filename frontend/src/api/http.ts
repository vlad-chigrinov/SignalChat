import type { ApiErrorCode } from './types'

const HTTP_NO_CONTENT = 204

/** Failed request. `code` is the stable ProblemDetails `code`, or null when the answer had none. */
export class ApiError extends Error {
  readonly status: number
  readonly code: ApiErrorCode | string | null

  constructor(status: number, message: string, code: ApiErrorCode | string | null = null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

/** Reads `code` from a ProblemDetails body. Anything else (HTML, empty, no code) gives null. */
export async function readProblemCode(response: Response): Promise<string | null> {
  try {
    const body: unknown = await response.json()
    if (typeof body !== 'object' || body === null) return null
    const code: unknown = Reflect.get(body, 'code')
    return typeof code === 'string' && code.length > 0 ? code : null
  } catch {
    return null
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
    const code = await readProblemCode(response)
    throw new ApiError(response.status, `${method} ${path} failed: ${response.status}`, code)
  }
  if (response.status === HTTP_NO_CONTENT) return undefined as T
  return (await response.json()) as T
}
