import { defineConfig, devices } from '@playwright/test'

import { REAL_BASE_URL, REAL_PORT } from './e2e/real/constants'

const SERVER_START_TIMEOUT_MS = 60_000
const TEST_TIMEOUT_MS = 60_000

/**
 * End-to-end runs against the REAL backend: two users chat through the SignalR hub. The API
 * must already run (./scripts/e2e-backend.ps1 or .sh); globalSetup stops the run with a hint
 * when it does not. Workers are 1: the scenarios share one backend and its database.
 */
export default defineConfig({
  testDir: './e2e/real',
  testMatch: '*.real.spec.ts',
  globalSetup: './e2e/real/globalSetup.ts',
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: TEST_TIMEOUT_MS,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-real' }]],
  use: {
    baseURL: REAL_BASE_URL,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `npm run dev -- --port ${REAL_PORT} --strictPort`,
    url: REAL_BASE_URL,
    reuseExistingServer: false,
    timeout: SERVER_START_TIMEOUT_MS,
    env: { VITE_USE_MOCK_API: 'false' },
  },
})
