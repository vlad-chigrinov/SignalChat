import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

import { describe, expect, it } from 'vitest'

import { buildContentSecurityPolicy } from './vite.csp.ts'

const SECURITY_HEADERS_PATH = fileURLToPath(
  new URL('./nginx-security-headers.conf', import.meta.url),
)
const CSP_HEADER_PATTERN = /add_header Content-Security-Policy "([^"]+)" always;/
/** Only an HTTP header can carry this directive: the <meta> tag built by vite.csp.ts cannot. */
const FRAME_ANCESTORS_DIRECTIVE = "frame-ancestors 'none'"

function securityHeadersConfig(): string {
  return readFileSync(SECURITY_HEADERS_PATH, 'utf8')
}

describe('nginx security headers', () => {
  it('send the same policy as the built app, plus frame-ancestors', () => {
    const headerValue = CSP_HEADER_PATTERN.exec(securityHeadersConfig())?.[1]

    expect(headerValue).toBe(`${buildContentSecurityPolicy()}; ${FRAME_ANCESTORS_DIRECTIVE}`)
  })

  it('leave Strict-Transport-Security to Traefik and the backend', () => {
    expect(securityHeadersConfig()).not.toMatch(/add_header\s+Strict-Transport-Security/i)
  })
})
