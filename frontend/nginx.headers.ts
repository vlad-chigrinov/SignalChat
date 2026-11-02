import { buildContentSecurityPolicy } from './vite.csp.ts'

const CSP_HEADER_PATTERN = /add_header Content-Security-Policy "[^"]+" always;/
/** Only an HTTP header can carry this directive: the <meta> tag built by vite.csp.ts cannot. */
const FRAME_ANCESTORS_DIRECTIVE = "frame-ancestors 'none'"

/**
 * Writes the policy for the API origin into the committed nginx-security-headers.conf. The committed file holds the
 * policy for an app whose API shares its origin; an image built for an API on its own origin gets the rendered copy.
 */
export function renderSecurityHeaders(template: string, apiOrigin: string): string {
  if (!CSP_HEADER_PATTERN.test(template)) {
    throw new Error('The security headers template has no Content-Security-Policy line to render.')
  }
  if (apiOrigin === '') return template

  const policy = `${buildContentSecurityPolicy(apiOrigin)}; ${FRAME_ANCESTORS_DIRECTIVE}`
  return template.replace(
    CSP_HEADER_PATTERN,
    () => `add_header Content-Security-Policy "${policy}" always;`,
  )
}
