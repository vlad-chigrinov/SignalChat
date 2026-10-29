/**
 * The real backend allows only this origin for the hub (CORS_ALLOWED_ORIGINS), so the real
 * e2e run uses the Vite dev port 5173. The mock run uses another port and never shares it.
 */
export const REAL_PORT = 5173
export const REAL_BASE_URL = `http://localhost:${REAL_PORT}`

export const BACKEND_ORIGIN = 'https://localhost:5001'
export const BACKEND_HEALTH_PATH = '/health'
export const TEST_LOGIN_PATH = '/auth/test-login'
