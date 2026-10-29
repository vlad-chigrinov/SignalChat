import { expect, test } from '@playwright/test'

import { REAL_BASE_URL } from './constants'
import { messageLog, sendMessage } from './ui'
import { startUser } from './users'

const ONLINE_LABEL = 'в сети'
const CHAT_PATH_PATTERN = /\/chat\/([^/?#]+)$/

test('a direct chat opens from search, shows presence and delivers messages', async ({
  browser,
}) => {
  const alice = await startUser(browser, 'alice')
  const bob = await startUser(browser, 'bob')
  try {
    await alice.page.getByRole('button', { name: 'Новое сообщение' }).click()
    await alice.page.getByLabel('Имя собеседника').fill(bob.handle)
    await alice.page.getByRole('button', { name: `Написать: ${bob.handle}` }).click()
    await expect(alice.page.getByRole('heading', { level: 1, name: bob.handle })).toBeVisible()

    // Presence is a snapshot taken on connect: reloading picks up Bob, who shares this chat now.
    await alice.page.reload()
    await expect(alice.page.getByText(ONLINE_LABEL, { exact: true })).toBeVisible()

    const chatPath = new URL(alice.page.url()).pathname
    const chatId = chatPath.match(CHAT_PATH_PATTERN)?.[1]
    expect(chatId, `direct chat id in ${chatPath}`).toBeTruthy()

    await bob.page.goto(`${REAL_BASE_URL}${chatPath}`)
    await expect(bob.page.getByRole('heading', { level: 1, name: alice.handle })).toBeVisible()

    await sendMessage(alice.page, 'личное сообщение')
    await expect(messageLog(bob.page).getByText('личное сообщение', { exact: true })).toBeVisible()
  } finally {
    await alice.context.close()
    await bob.context.close()
  }
})
