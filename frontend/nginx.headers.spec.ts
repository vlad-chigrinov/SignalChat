import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

import { describe, expect, it } from 'vitest'

import { renderSecurityHeaders } from './nginx.headers.ts'
import { buildContentSecurityPolicy } from './vite.csp.ts'

const TEMPLATE = readFileSync(
  fileURLToPath(new URL('./nginx-security-headers.conf', import.meta.url)),
  'utf8',
)
const API_ORIGIN = 'https://chat-api.vlados.tech'
const CSP_HEADER_PATTERN = /add_header Content-Security-Policy "([^"]+)" always;/

describe('renderSecurityHeaders', () => {
  it('writes the policy for the API origin into the header, with frame-ancestors', () => {
    const rendered = renderSecurityHeaders(TEMPLATE, API_ORIGIN)

    expect(CSP_HEADER_PATTERN.exec(rendered)?.[1]).toBe(
      `${buildContentSecurityPolicy(API_ORIGIN)}; frame-ancestors 'none'`,
    )
  })

  it('leaves the file as it is when the API shares the origin', () => {
    expect(renderSecurityHeaders(TEMPLATE, '')).toBe(TEMPLATE)
  })

  it('keeps every other header', () => {
    const rendered = renderSecurityHeaders(TEMPLATE, API_ORIGIN)

    for (const header of [
      'X-Content-Type-Options',
      'X-Frame-Options',
      'Referrer-Policy',
      'Permissions-Policy',
    ]) {
      expect(rendered).toContain(`add_header ${header} `)
    }
  })

  it('refuses a template without a policy line instead of shipping no CSP', () => {
    expect(() =>
      renderSecurityHeaders('add_header X-Frame-Options "DENY" always;', API_ORIGIN),
    ).toThrow(/Content-Security-Policy/)
  })
})
