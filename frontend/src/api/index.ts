import type { ChatApi } from './contract'
import { httpApi } from './httpApi'
import { createMockApi } from './mock/mockApi'

/**
 * The dev server talks to the real backend (through the Vite proxy) by default. Set
 * VITE_USE_MOCK_API=true in frontend/.env to run on the in-memory mocks instead. Production
 * builds never use the mocks (DEV is false there, so the mock code is dropped).
 */
export const USE_MOCK_API = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK_API === 'true'

export const api: ChatApi = USE_MOCK_API
  ? createMockApi({ startSignedOut: import.meta.env.VITE_MOCK_START_SIGNED_OUT === 'true' })
  : httpApi

export type { ChatApi } from './contract'
export { ApiError } from './http'
export type * from './types'
