/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** "false" switches `npm run dev` from mocks to the real backend. */
  readonly VITE_USE_MOCK_API?: string
  /** Origin of the API when it is not the SPA's own, e.g. https://chat-api.vlados.tech. Empty in development. */
  readonly VITE_API_ORIGIN?: string
  /** "true": the mock API starts without a session (used by e2e). */
  readonly VITE_MOCK_START_SIGNED_OUT?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
