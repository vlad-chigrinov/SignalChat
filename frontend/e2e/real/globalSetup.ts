import { request as httpsRequest } from 'node:https'

import { BACKEND_HEALTH_PATH, BACKEND_ORIGIN, TEST_LOGIN_PATH } from './constants'

const PROBE_TIMEOUT_MS = 5000
const HTTP_SUCCESS_MIN = 200
const HTTP_SUCCESS_MAX = 299
const START_HINT =
  'Start the API first in another terminal: ./scripts/e2e-backend.ps1 (Windows) or ./scripts/e2e-backend.sh (macOS / Linux / Git Bash). Then run npm run test:e2e:real again.'

/**
 * Answers the HTTP status of a probe, or null when the API cannot be reached at all.
 * The dev certificate is trusted by Windows but not by Node, so certificate checks are off for
 * this one probe of localhost: no test data is sent over it.
 */
function probe(method: string, path: string, body?: string): Promise<number | null> {
  return new Promise((resolve) => {
    const url = new URL(path, BACKEND_ORIGIN)
    const request = httpsRequest(
      url,
      {
        method,
        rejectUnauthorized: false,
        timeout: PROBE_TIMEOUT_MS,
        headers: body ? { 'Content-Type': 'application/json' } : undefined,
      },
      (response) => {
        response.resume()
        resolve(response.statusCode ?? null)
      },
    )
    request.on('timeout', () => request.destroy())
    request.on('error', () => resolve(null))
    request.end(body)
  })
}

function isSuccess(status: number | null): boolean {
  return status !== null && status >= HTTP_SUCCESS_MIN && status <= HTTP_SUCCESS_MAX
}

/** Fails before any test runs, with the command that fixes the problem. */
export default async function globalSetup(): Promise<void> {
  const health = await probe('GET', BACKEND_HEALTH_PATH)
  if (!isSuccess(health)) {
    throw new Error(
      `The API is not reachable at ${BACKEND_ORIGIN}${BACKEND_HEALTH_PATH} (status: ${health ?? 'no connection'}). ${START_HINT}`,
    )
  }

  const testLogin = await probe('POST', TEST_LOGIN_PATH, JSON.stringify({ handle: 'globalsetup' }))
  if (!isSuccess(testLogin)) {
    throw new Error(
      `Test sign-in is not enabled on ${BACKEND_ORIGIN} (status: ${testLogin ?? 'no connection'}). The API must start with E2E_TEST_AUTH=true. ${START_HINT}`,
    )
  }
}
