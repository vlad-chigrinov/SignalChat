import { expect, test, type Browser } from '@playwright/test'

import { startUser, uniqueSuffix, type RealUser } from './users'
import {
  createRoom,
  joinRoomFromCatalog,
  messageLog,
  sendMessage,
  typeWithoutSending,
  typingLine,
} from './ui'

const HTML_TEXT = '<img src=x onerror="window.__xss = true">'

interface RoomPair {
  alice: RealUser
  bob: RealUser
  roomName: string
}

/** Alice creates a room, Bob finds it in the catalog and joins: both are members. */
async function roomWithTwoMembers(browser: Browser): Promise<RoomPair> {
  const alice = await startUser(browser, 'alice')
  const bob = await startUser(browser, 'bob')
  const roomName = `Тест ${uniqueSuffix()}`
  await createRoom(alice.page, roomName)
  await joinRoomFromCatalog(bob.page, roomName)
  return { alice, bob, roomName }
}

test.describe('room chat between two real users', () => {
  test('messages reach the other member without a reload, and typing shows', async ({
    browser,
  }) => {
    const { alice, bob } = await roomWithTwoMembers(browser)
    try {
      await sendMessage(alice.page, 'привет от alice')
      await expect(messageLog(bob.page).getByText('привет от alice', { exact: true })).toBeVisible()

      await sendMessage(bob.page, 'привет от bob')
      await expect(messageLog(alice.page).getByText('привет от bob', { exact: true })).toBeVisible()

      await typeWithoutSending(bob.page, 'пишу')
      await expect(typingLine(alice.page)).toBeVisible()
    } finally {
      await alice.context.close()
      await bob.context.close()
    }
  })

  test('a member who leaves the room loses it from their list and cannot open it', async ({
    browser,
  }) => {
    const { alice, bob, roomName } = await roomWithTwoMembers(browser)
    try {
      const roomUrl = bob.page.url()
      await bob.page.getByRole('button', { name: 'Покинуть комнату' }).click()
      await expect(bob.page.getByRole('heading', { name: 'Комнаты' })).toBeVisible()
      await expect(bob.page.getByRole('link', { name: new RegExp(roomName) })).toHaveCount(0)

      // Bob is no longer a member, so the room is not found for him, and Alice still writes.
      await sendMessage(alice.page, 'после выхода боба')
      await bob.page.goto(roomUrl)
      await expect(bob.page.getByText('Чат не найден или у вас нет к нему доступа.')).toBeVisible()
    } finally {
      await alice.context.close()
      await bob.context.close()
    }
  })

  test('text with HTML characters is shown as plain text', async ({ browser }) => {
    const { alice, bob } = await roomWithTwoMembers(browser)
    try {
      await sendMessage(alice.page, HTML_TEXT)
      await expect(messageLog(bob.page).getByText(HTML_TEXT, { exact: true })).toBeVisible()

      const injected = await bob.page.evaluate(
        () => (window as unknown as { __xss?: boolean }).__xss === true,
      )
      expect(injected).toBe(false)
      await expect(bob.page.locator('img[src="x"]')).toHaveCount(0)
    } finally {
      await alice.context.close()
      await bob.context.close()
    }
  })
})
