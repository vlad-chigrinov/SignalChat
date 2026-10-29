import { expect, test } from '@playwright/test'

/**
 * Skeleton for ticket #7 (SignalR client): two browser contexts = two users.
 *
 * TODO(#7): enable once the real backend and hub are available:
 *  - run with VITE_USE_MOCK_API=false against a backend using a test auth scheme
 *    (no Google keys), giving each context its own cookie / identity;
 *  - sign each context in as a different user before opening the chat.
 * The in-memory mock lives inside each page, so this scenario cannot pass on mocks.
 */
test.fixme('a message sent by user A appears for user B in real time', async ({ browser }) => {
  const alice = await browser.newContext()
  const bob = await browser.newContext()
  const alicePage = await alice.newPage()
  const bobPage = await bob.newPage()

  await Promise.all([alicePage.goto('/chat/room-general'), bobPage.goto('/chat/room-general')])

  const text = `realtime ${Date.now()}`
  await alicePage.getByLabel('Сообщение').fill(text)
  await alicePage.getByRole('button', { name: 'Отправить' }).click()

  await expect(bobPage.getByRole('log', { name: 'Сообщения' }).getByText(text)).toBeVisible()

  await alice.close()
  await bob.close()
})
