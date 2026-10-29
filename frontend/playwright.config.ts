import { defineConfig, devices } from '@playwright/test'

const E2E_PORT = 5174
const E2E_BASE_URL = `http://localhost:${E2E_PORT}`
const SERVER_START_TIMEOUT_MS = 60_000

/**
 * E2E runs against the Vite dev server with the in-memory mock API (no backend, no Google keys).
 * The mock starts signed out so the sign-in flow and the route guard can be exercised.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: E2E_BASE_URL,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `npm run dev -- --port ${E2E_PORT} --strictPort`,
    url: E2E_BASE_URL,
    reuseExistingServer: false,
    timeout: SERVER_START_TIMEOUT_MS,
    env: { VITE_USE_MOCK_API: 'true', VITE_MOCK_START_SIGNED_OUT: 'true' },
  },
})
