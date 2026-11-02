import { describe, expect, it } from 'vitest'

import {
  buildContentSecurityPolicy,
  contentSecurityPolicyPlugin,
  contentSecurityPolicyTag,
} from './vite.csp.ts'

/** Parses the policy into `directive → sources`, the way the browser reads it. */
function parsePolicy(policy: string): Map<string, string[]> {
  return new Map(
    policy.split(';').map((part) => {
      const [name, ...sources] = part.trim().split(/\s+/)
      return [name, sources]
    }),
  )
}

describe('content security policy', () => {
  const policy = parsePolicy(buildContentSecurityPolicy())

  it('allows scripts and styles only from this origin', () => {
    expect(policy.get('script-src')).toEqual(["'self'"])
    expect(policy.get('style-src')).toEqual(["'self'"])
    expect(policy.get('default-src')).toEqual(["'self'"])
  })

  it('never allows inline or eval scripts', () => {
    const scriptSources = policy.get('script-src') ?? []
    expect(scriptSources).not.toContain("'unsafe-inline'")
    expect(scriptSources).not.toContain("'unsafe-eval'")
  })

  it('allows Google avatar images and nothing else external', () => {
    expect(policy.get('img-src')).toEqual(["'self'", 'https://*.googleusercontent.com'])
  })

  it('blocks plugins and base-tag hijacking', () => {
    expect(policy.get('object-src')).toEqual(["'none'"])
    expect(policy.get('base-uri')).toEqual(["'none'"])
  })

  it('leaves out frame-ancestors, which a meta tag ignores', () => {
    expect(policy.has('frame-ancestors')).toBe(false)
  })
})

describe('content security policy with an API on its own origin', () => {
  const policy = parsePolicy(buildContentSecurityPolicy('https://chat-api.vlados.tech'))

  it('lets the app connect to that origin over https and wss, and nowhere else', () => {
    expect(policy.get('connect-src')).toEqual([
      "'self'",
      'https://chat-api.vlados.tech',
      'wss://chat-api.vlados.tech',
    ])
  })

  it('changes nothing else', () => {
    const plain = parsePolicy(buildContentSecurityPolicy())
    for (const name of plain.keys()) {
      if (name !== 'connect-src') expect(policy.get(name)).toEqual(plain.get(name))
    }
  })

  it('is the default policy when the API shares the origin', () => {
    expect(buildContentSecurityPolicy('')).toBe(buildContentSecurityPolicy())
  })
})

describe('content security policy tag', () => {
  it('is a meta tag placed first in the head, with the same policy', () => {
    const tag = contentSecurityPolicyTag()

    expect(tag).toMatchObject({
      tag: 'meta',
      injectTo: 'head-prepend',
      attrs: { 'http-equiv': 'Content-Security-Policy' },
    })
    expect(tag.attrs?.content).toBe(buildContentSecurityPolicy())
  })

  it('is added to the production build only', () => {
    expect(contentSecurityPolicyPlugin().apply).toBe('build')
  })
})
