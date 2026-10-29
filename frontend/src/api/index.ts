import type { ChatApi } from './contract'
import { httpApi } from './httpApi'
import { createMockApi } from './mock/mockApi'

/**
 * Mocks are on by default in `npm run dev` until the backend REST API exists.
 * Set VITE_USE_MOCK_API=false in frontend/.env to talk to the real backend.
 * Production builds always use the HTTP client (the mock module is tree-shaken away).
 */
export const USE_MOCK_API = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK_API !== 'false'

export const api: ChatApi = USE_MOCK_API
  ? createMockApi({ startSignedOut: import.meta.env.VITE_MOCK_START_SIGNED_OUT === 'true' })
  : httpApi

export type { ChatApi } from './contract'
export { ApiError } from './http'
export type * from './types'
