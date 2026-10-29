import * as signalR from '@microsoft/signalr'
import { describe, expect, it } from 'vitest'

import { RECONNECT_DELAYS_MS } from '../hubContract'
import { reconnectPolicy } from '../reconnectPolicy'

const HTTP_UNAUTHORIZED = 401
const HTTP_BAD_GATEWAY = 502
const LAST_DELAY_MS = RECONNECT_DELAYS_MS[RECONNECT_DELAYS_MS.length - 1]

function nextDelay(previousRetryCount: number, retryReason: Error) {
  return reconnectPolicy.nextRetryDelayInMilliseconds({
    previousRetryCount,
    retryReason,
    elapsedMilliseconds: 0,
  })
}

describe('reconnect policy', () => {
  it('walks the configured delays in order', () => {
    RECONNECT_DELAYS_MS.forEach((delay, attempt) => {
      expect(nextDelay(attempt, new Error('network'))).toBe(delay)
    })
  })

  it('keeps retrying with the last delay after the list runs out', () => {
    const afterList = RECONNECT_DELAYS_MS.length + 3

    expect(nextDelay(afterList, new Error('network'))).toBe(LAST_DELAY_MS)
  })

  it('stops at once when the session is gone (401)', () => {
    const reason = new signalR.HttpError('unauthorized', HTTP_UNAUTHORIZED)

    expect(nextDelay(0, reason)).toBeNull()
  })

  it('keeps retrying other HTTP failures such as a gateway error', () => {
    const reason = new signalR.HttpError('bad gateway', HTTP_BAD_GATEWAY)

    expect(nextDelay(0, reason)).toBe(RECONNECT_DELAYS_MS[0])
  })
})
