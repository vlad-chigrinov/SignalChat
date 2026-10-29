const FALLBACK_PATH = '/'

/**
 * Keeps only same-origin relative paths, so a crafted `?redirect=` cannot send
 * the user to another site after sign-in (open redirect).
 */
export function safeReturnPath(candidate: unknown): string {
  if (typeof candidate !== 'string') return FALLBACK_PATH
  const isRelative = candidate.startsWith('/')
  const isProtocolRelative = candidate.startsWith('//') || candidate.startsWith('/\\')
  return isRelative && !isProtocolRelative ? candidate : FALLBACK_PATH
}
