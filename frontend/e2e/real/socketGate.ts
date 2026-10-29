import type { Page, WebSocketRoute } from '@playwright/test'

const HUB_SOCKET_PATTERN = /\/hubs\/chat/
const HUB_NEGOTIATE_PATTERN = /\/hubs\/chat\/negotiate/

export interface SocketGate {
  /** Drops the live hub socket and refuses new ones (the client sees a lost connection). */
  cut(): void
  /** Lets the client reconnect again. */
  restore(): void
}

/**
 * Simulates a network outage for the hub only: the page's WebSocket is closed and the negotiate
 * step is aborted while cut. Must be installed before the page opens the hub.
 */
export function installSocketGate(page: Page): SocketGate {
  let isCut = false
  let liveSockets: WebSocketRoute[] = []

  void page.routeWebSocket(HUB_SOCKET_PATTERN, (socket) => {
    if (isCut) {
      void socket.close()
      return
    }
    socket.connectToServer()
    liveSockets.push(socket)
  })
  void page.route(HUB_NEGOTIATE_PATTERN, (route) => (isCut ? route.abort() : route.continue()))

  return {
    cut() {
      isCut = true
      liveSockets.forEach((socket) => void socket.close())
      liveSockets = []
    },
    restore() {
      isCut = false
    },
  }
}
