import { describe, expect, it } from 'vitest'

import { credentialsFor, joinApiUrl, normalizeApiOrigin, webSocketOriginOf } from '../apiOrigin'

describe('normalizeApiOrigin', () => {
  it('means "same origin" when nothing is set', () => {
    expect(normalizeApiOrigin(undefined)).toBe('')
    expect(normalizeApiOrigin('')).toBe('')
    expect(normalizeApiOrigin('   ')).toBe('')
  })

  it('keeps an https origin and drops the trailing slash', () => {
    expect(normalizeApiOrigin('https://chat-api.vlados.tech')).toBe('https://chat-api.vlados.tech')
    expect(normalizeApiOrigin('https://chat-api.vlados.tech/')).toBe('https://chat-api.vlados.tech')
  })

  it('refuses anything that is not a bare http(s) origin', () => {
    expect(() => normalizeApiOrigin('chat-api.vlados.tech')).toThrow(/VITE_API_ORIGIN/)
    expect(() => normalizeApiOrigin('ftp://chat-api.vlados.tech')).toThrow(/VITE_API_ORIGIN/)
    expect(() => normalizeApiOrigin('https://chat-api.vlados.tech/api')).toThrow(/VITE_API_ORIGIN/)
    expect(() => normalizeApiOrigin('javascript:alert(1)')).toThrow(/VITE_API_ORIGIN/)
  })
})

describe('joinApiUrl', () => {
  it('stays a relative path without an origin', () => {
    expect(joinApiUrl('', '/api/rooms')).toBe('/api/rooms')
  })

  it('points at the API origin when one is set', () => {
    expect(joinApiUrl('https://chat-api.vlados.tech', '/api/rooms')).toBe(
      'https://chat-api.vlados.tech/api/rooms',
    )
  })
})

describe('credentialsFor', () => {
  it('sends the session cookie to another origin only on purpose', () => {
    expect(credentialsFor('')).toBe('same-origin')
    expect(credentialsFor('https://chat-api.vlados.tech')).toBe('include')
  })
})

describe('webSocketOriginOf', () => {
  it('maps https to wss and http to ws', () => {
    expect(webSocketOriginOf('https://chat-api.vlados.tech')).toBe('wss://chat-api.vlados.tech')
    expect(webSocketOriginOf('http://localhost:5001')).toBe('ws://localhost:5001')
  })
})
