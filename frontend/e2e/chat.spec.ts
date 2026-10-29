import { expect, test } from '@playwright/test'

import { openChat, signIn } from './helpers'

const MAX_WHEEL_DELTA_PX = 100_000

test('sign in, open a chat, send a message, load older history', async ({ page }) => {
  await signIn(page)
  await expect(page).toHaveURL('/')
  await expect(page.getByRole('heading', { name: 'Личные сообщения' })).toBeVisible()

  await openChat(page, 'Общий')
  const log = page.getByRole('log', { name: 'Сообщения' })
  await expect(log.getByText('(#95)')).toBeVisible()

  const text = `e2e привет ${Date.now()}`
  await page.getByLabel('Сообщение').fill(text)
  await page.getByRole('button', { name: 'Отправить' }).click()
  await expect(log.getByText(text)).toBeVisible()
  await expect(page.getByLabel('Сообщение')).toHaveValue('')

  // The mock serves 95 messages in pages: scrolling up must fetch the earlier ones.
  await expect(log.getByText('(#1)', { exact: false })).toHaveCount(0)
  await expect(async () => {
    await log.hover()
    await page.mouse.wheel(0, -MAX_WHEEL_DELTA_PX)
    await expect(log.getByText('Начало истории')).toBeVisible({ timeout: 1000 })
  }).toPass()
  await expect(log.getByText('(#1)', { exact: false })).toBeAttached()
})

test('messages are rendered as plain text (no HTML injection)', async ({ page }) => {
  await signIn(page)
  await openChat(page, 'Общий')
  const payload = '<img src=x onerror="window.__xss = true">'
  await page.getByLabel('Сообщение').fill(payload)
  await page.getByRole('button', { name: 'Отправить' }).click()

  const log = page.getByRole('log', { name: 'Сообщения' })
  await expect(log.getByText(payload)).toBeVisible()
  await expect(log.locator('img')).toHaveCount(0)
  expect(await page.evaluate(() => (window as { __xss?: boolean }).__xss)).toBeUndefined()
})
