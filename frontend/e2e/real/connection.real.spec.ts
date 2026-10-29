import { expect, test } from '@playwright/test'

import { createRoom, connectionBanner, joinRoomFromCatalog, messageLog, sendMessage } from './ui'
import { installSocketGate } from './socketGate'
import { startUser, uniqueSuffix } from './users'

/** Reconnect delays grow to 30 s (docs/realtime-hub.md), so the waits are generous. */
const CONNECTION_TEST_TIMEOUT_MS = 120_000
const BANNER_SHOWN_TIMEOUT_MS = 20_000
const BANNER_HIDDEN_TIMEOUT_MS = 60_000
const CATCH_UP_TIMEOUT_MS = 15_000

test('a lost connection shows a banner, and the missed message arrives after reconnect', async ({
  browser,
}) => {
  test.setTimeout(CONNECTION_TEST_TIMEOUT_MS)
  const alice = await startUser(browser, 'alice')
  const bob = await startUser(browser, 'bob', installSocketGate)
  try {
    const roomName = `Связь ${uniqueSuffix()}`
    await createRoom(alice.page, roomName)
    await joinRoomFromCatalog(bob.page, roomName)

    bob.extra.cut()
    await expect(connectionBanner(bob.page)).toBeVisible({ timeout: BANNER_SHOWN_TIMEOUT_MS })

    const missedText = 'написано, пока у боба нет связи'
    await sendMessage(alice.page, missedText)

    bob.extra.restore()
    await expect(connectionBanner(bob.page)).toBeHidden({ timeout: BANNER_HIDDEN_TIMEOUT_MS })
    await expect(messageLog(bob.page).getByText(missedText, { exact: true })).toBeVisible({
      timeout: CATCH_UP_TIMEOUT_MS,
    })
  } finally {
    await alice.context.close()
    await bob.context.close()
  }
})
