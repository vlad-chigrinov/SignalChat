import { fileURLToPath, URL } from 'node:url'

import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

import { contentSecurityPolicyPlugin } from './vite.csp.ts'
import { createBackendProxy, DEFAULT_BACKEND_URL } from './vite.proxy.ts'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Empty prefix: read BACKEND_URL without exposing it to client code (no VITE_ prefix).
  const env = loadEnv(mode, process.cwd(), '')
  const backendUrl = env.BACKEND_URL || DEFAULT_BACKEND_URL

  return {
    plugins: [vue(), vueDevTools(), contentSecurityPolicyPlugin()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      proxy: createBackendProxy(backendUrl),
    },
  }
})
