import { expect, test, type Locator, type Page } from '@playwright/test'

import { openChat, signIn } from './helpers'

const PHONE_VIEWPORT = { width: 375, height: 667 }
/** In the mock seed, the newest page of "Общий" holds these messages (the text ends with `(#n)`). */
const PLAIN_MESSAGE = '(#91)'
const SINGLE_REACTION_MESSAGE = '(#92)'
const SHARED_REACTION_MESSAGE = '(#93)'

function messageWith(page: Page, marker: string): Locator {
  return page
    .getByRole('log', { name: 'Сообщения' })
    .locator('article', { hasText: marker })
    .first()
}

function picker(page: Page): Locator {
  return page.getByRole('group', { name: 'Выберите реакцию' })
}

async function openGeneral(page: Page): Promise<void> {
  await signIn(page)
  await openChat(page, 'Общий')
  await expect(messageWith(page, PLAIN_MESSAGE)).toBeVisible()
}

async function react(page: Page, marker: string, emoji: string): Promise<void> {
  await messageWith(page, marker).locator('.text').click()
  await picker(page).getByRole('button', { name: emoji }).click()
}

function chip(message: Locator, emoji: string): Locator {
  return message.getByRole('button', { name: new RegExp(`^${emoji}`) })
}

test('a click on a message opens the panel; choosing an emoji puts it under the message', async ({
  page,
}) => {
  await openGeneral(page)
  const message = messageWith(page, PLAIN_MESSAGE)

  await message.locator('.text').click()
  await expect(picker(page)).toBeVisible()
  await picker(page).getByRole('button', { name: '👍' }).click()

  await expect(picker(page)).toHaveCount(0)
  const mine = chip(message, '👍')
  await expect(mine).toBeVisible()
  await expect(mine).toHaveAttribute('aria-pressed', 'true')
  // One person: the emoji alone, no number.
  await expect(mine).toHaveText('👍')
})

test('choosing another emoji replaces the first one', async ({ page }) => {
  await openGeneral(page)
  const message = messageWith(page, PLAIN_MESSAGE)
  await react(page, PLAIN_MESSAGE, '👍')
  await expect(chip(message, '👍')).toBeVisible()

  await react(page, PLAIN_MESSAGE, '❤️')

  await expect(chip(message, '❤️')).toHaveAttribute('aria-pressed', 'true')
  await expect(chip(message, '👍')).toHaveCount(0)
  await expect(message.getByRole('list', { name: 'Реакции' }).getByRole('listitem')).toHaveCount(1)
})

test('pressing the own reaction takes it back', async ({ page }) => {
  await openGeneral(page)
  const message = messageWith(page, PLAIN_MESSAGE)
  await react(page, PLAIN_MESSAGE, '🎉')
  await expect(chip(message, '🎉')).toBeVisible()

  await chip(message, '🎉').click()

  await expect(message.getByRole('list', { name: 'Реакции' })).toHaveCount(0)
})

test('choosing the same emoji in the panel again also takes it back', async ({ page }) => {
  await openGeneral(page)
  const message = messageWith(page, PLAIN_MESSAGE)
  await react(page, PLAIN_MESSAGE, '😮')
  await expect(chip(message, '😮')).toBeVisible()

  await react(page, PLAIN_MESSAGE, '😮')

  await expect(message.getByRole('list', { name: 'Реакции' })).toHaveCount(0)
})

test('the same emoji from two people shows a count; a single one shows none', async ({ page }) => {
  await openGeneral(page)
  const shared = messageWith(page, SHARED_REACTION_MESSAGE)
  const single = messageWith(page, SINGLE_REACTION_MESSAGE)

  // Seed: one ❤️ on the single message; a 👍 and a 😂 on the shared one.
  await expect(chip(single, '❤️')).toHaveText('❤️')
  await expect(chip(shared, '👍')).toHaveText('👍')
  await expect(chip(shared, '👍')).toHaveAttribute('aria-pressed', 'false')

  await react(page, SHARED_REACTION_MESSAGE, '👍')

  await expect(chip(shared, '👍')).toHaveText(/👍\s*2/)
  await expect(chip(shared, '👍')).toHaveAttribute('aria-pressed', 'true')
  await expect(chip(shared, '😂')).toHaveText('😂')

  await chip(shared, '👍').click()
  await expect(chip(shared, '👍')).toHaveText('👍')
  await expect(chip(shared, '👍')).toHaveAttribute('aria-pressed', 'false')
})

test('the panel closes on Escape and on a click outside it', async ({ page }) => {
  await openGeneral(page)

  await messageWith(page, PLAIN_MESSAGE).locator('.text').click()
  await expect(picker(page)).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(picker(page)).toHaveCount(0)

  await messageWith(page, PLAIN_MESSAGE).locator('.text').click()
  await expect(picker(page)).toBeVisible()
  await page.getByRole('heading', { level: 1 }).click()
  await expect(picker(page)).toHaveCount(0)
})

test('a click on another message moves the panel there', async ({ page }) => {
  await openGeneral(page)

  await messageWith(page, PLAIN_MESSAGE).locator('.text').click()
  await messageWith(page, SINGLE_REACTION_MESSAGE).locator('.text').click()

  await expect(picker(page)).toHaveCount(1)
  await expect(messageWith(page, SINGLE_REACTION_MESSAGE).getByRole('group')).toBeVisible()
})

test('selecting text does not open the panel', async ({ page }) => {
  await openGeneral(page)
  const text = messageWith(page, PLAIN_MESSAGE).locator('.text')
  const box = await text.boundingBox()
  if (!box) throw new Error('the message text has no box')

  await page.mouse.move(box.x + 1, box.y + box.height / 2)
  await page.mouse.down()
  await page.mouse.move(box.x + box.width - 1, box.y + box.height / 2, { steps: 5 })
  await page.mouse.up()

  expect(await page.evaluate(() => window.getSelection()?.toString().length)).toBeGreaterThan(0)
  await expect(picker(page)).toHaveCount(0)
})

test('the keyboard reaches the panel through the reaction button', async ({ page }) => {
  await openGeneral(page)
  const message = messageWith(page, PLAIN_MESSAGE)

  await message.getByRole('button', { name: 'Добавить реакцию' }).focus()
  await page.keyboard.press('Enter')
  await expect(picker(page)).toBeVisible()
  await page.keyboard.press('Escape')

  await expect(picker(page)).toHaveCount(0)
  await expect(message.getByRole('button', { name: 'Добавить реакцию' })).toBeFocused()
})

test('on a phone the panel stays inside the screen', async ({ page }) => {
  await page.setViewportSize(PHONE_VIEWPORT)
  await openGeneral(page)

  await messageWith(page, PLAIN_MESSAGE).locator('.text').click()

  const box = await picker(page).boundingBox()
  if (!box) throw new Error('the panel has no box')
  expect(box.x).toBeGreaterThanOrEqual(0)
  expect(box.x + box.width).toBeLessThanOrEqual(PHONE_VIEWPORT.width)
  await picker(page).getByRole('button', { name: '🎉' }).click()
  await expect(chip(messageWith(page, PLAIN_MESSAGE), '🎉')).toBeVisible()
})
