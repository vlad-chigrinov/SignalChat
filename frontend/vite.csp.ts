import type { HtmlTagDescriptor, Plugin } from 'vite'

import { webSocketOriginOf } from './src/api/apiOrigin.ts'

/**
 * Content-Security-Policy of the built app. Scripts and styles come only from this origin, so
 * an injected inline script cannot run. No 'unsafe-inline' and no 'unsafe-eval' for scripts.
 * When the API has an origin of its own, the app may connect to that origin (https and wss) and nowhere else.
 */
function cspDirectives(apiOrigin: string): Record<string, readonly string[]> {
  const connectSources = apiOrigin === '' ? [] : [apiOrigin, webSocketOriginOf(apiOrigin)]
  const apiSources = apiOrigin === '' ? [] : [apiOrigin]
  return {
    'default-src': ["'self'"],
    'script-src': ["'self'"],
    'style-src': ["'self'"],
    // Avatars come from Google or, when the API has an origin of its own, from the API (see UserAvatar.vue).
    // blob: is the preview of a photo the user picked: the browser's own copy, shown before anything is sent.
    'img-src': ["'self'", 'blob:', 'https://*.googleusercontent.com', ...apiSources],
    'font-src': ["'self'"],
    'connect-src': ["'self'", ...connectSources],
    'object-src': ["'none'"],
    'base-uri': ["'none'"],
    'form-action': ["'self'"],
  }
}

/**
 * `frame-ancestors` is ignored in a <meta> tag: the backend must send it as an HTTP header
 * (`Content-Security-Policy: frame-ancestors 'none'`), together with the other headers.
 */
export function buildContentSecurityPolicy(apiOrigin = ''): string {
  return Object.entries(cspDirectives(apiOrigin))
    .map(([name, sources]) => `${name} ${sources.join(' ')}`)
    .join('; ')
}

/** Only the production build gets this tag: the dev server injects inline scripts for HMR. */
export function contentSecurityPolicyTag(apiOrigin = ''): HtmlTagDescriptor {
  return {
    tag: 'meta',
    attrs: {
      'http-equiv': 'Content-Security-Policy',
      content: buildContentSecurityPolicy(apiOrigin),
    },
    injectTo: 'head-prepend',
  }
}

export function contentSecurityPolicyPlugin(apiOrigin = ''): Plugin {
  return {
    name: 'signalchat-content-security-policy',
    apply: 'build',
    transformIndexHtml: () => [contentSecurityPolicyTag(apiOrigin)],
  }
}
