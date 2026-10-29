/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** "false" switches `npm run dev` from mocks to the real backend. */
  readonly VITE_USE_MOCK_API?: string
  /** "true": the mock API starts without a session (used by e2e). */
  readonly VITE_MOCK_START_SIGNED_OUT?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
