import * as signalR from '@microsoft/signalr'

import { retryDelayMs } from './retryDelay'

const HTTP_UNAUTHORIZED = 401

/**
 * Automatic reconnect that never gives up on its own: it keeps retrying with the last delay.
 * A 401 on the negotiate step means the session is gone, so retrying cannot help: it stops
 * at once and the connection closes, which the service reports as a session end.
 */
export const reconnectPolicy: signalR.IRetryPolicy = {
  nextRetryDelayInMilliseconds({ previousRetryCount, retryReason }) {
    if (retryReason instanceof signalR.HttpError && retryReason.statusCode === HTTP_UNAUTHORIZED) {
      return null
    }
    return retryDelayMs(previousRetryCount)
  },
}
