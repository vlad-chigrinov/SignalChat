import { expect, test, type Locator, type Page } from '@playwright/test'

import { openChat, signIn } from './helpers'
import { fakeHub } from './fakeHub'

const SINGLE_LINE_TOLERANCE_PX = 2
const MANY_LINES = Array.from({ length: 30 }, (_, index) => `строка ${index + 1}`).join('\n')
const MOBILE_VIEWPORT = { width: 320, height: 640 }

async function openGeneralChat(page: Page): Promise<Locator> {
  await signIn(page)
  await openChat(page, 'Общий')
  return page.getByLabel('Сообщение')
}

async function heightOf(field: Locator): Promise<number> {
  const box = await field.boundingBox()
  if (!box) throw new Error('The composer is not rendered')
  return box.height
}

test.describe('composer height', () => {
  test('grows with the text, stops at the maximum and scrolls inside', async ({ page }) => {
    const field = await openGeneralChat(page)
    const oneLine = await heightOf(field)

    await field.fill('первая\nвторая\nтретья')
    const threeLines = await heightOf(field)
    expect(threeLines).toBeGreaterThan(oneLine + SINGLE_LINE_TOLERANCE_PX)

    await field.fill(MANY_LINES)
    const capped = await heightOf(field)
    expect(capped).toBeGreaterThan(threeLines)
    await field.fill(`${MANY_LINES}\nещё строка\nи ещё`)
    expect(await heightOf(field)).toBe(capped)
    expect(await field.evaluate((el) => el.scrollHeight > el.clientHeight)).toBe(true)
  })

  test('shrinks back when text is deleted and after a send', async ({ page }) => {
    const field = await openGeneralChat(page)
    const oneLine = await heightOf(field)

    await field.fill(MANY_LINES)
    await field.fill('осталась одна строка')
    expect(await heightOf(field)).toBeCloseTo(oneLine, 0)

    await field.fill('первая\nвторая\nтретья')
    await page.getByRole('button', { name: 'Отправить' }).click()
    await expect(field).toHaveValue('')
    expect(await heightOf(field)).toBeCloseTo(oneLine, 0)
  })

  test('grows for a pasted long text on a narrow screen', async ({ page }) => {
    await page.setViewportSize(MOBILE_VIEWPORT)
    await signIn(page)
    await openChat(page, 'Общий')
    const field = page.getByLabel('Сообщение')
    const oneLine = await heightOf(field)

    // One line without line breaks: only the wrapping on a narrow screen makes it tall.
    await field.fill('длинное слово '.repeat(20))
    expect(await heightOf(field)).toBeGreaterThan(oneLine + SINGLE_LINE_TOLERANCE_PX)
  })

  test('Enter sends and Shift+Enter inserts a line break', async ({ page }) => {
    const field = await openGeneralChat(page)
    const log = page.getByRole('log', { name: 'Сообщения' })

    await field.fill('строка один')
    await field.press('Shift+Enter')
    await field.pressSequentially('строка два')
    await expect(field).toHaveValue('строка один\nстрока два')

    await field.press('Enter')
    await expect(field).toHaveValue('')
    await expect(log.getByText('строка два')).toBeVisible()
  })
})

test.describe('sending', () => {
  test('clears the field at once, keeps focus and sends only once while the hub is slow', async ({
    page,
  }) => {
    const field = await openGeneralChat(page)
    const log = page.getByRole('log', { name: 'Сообщения' })
    const text = `медленная отправка ${Date.now()}`
    await fakeHub(page).holdSends()

    await field.fill(text)
    await page.getByRole('button', { name: 'Отправить' }).click()
    await expect(field).toHaveValue('')
    await expect(field).toBeFocused()
    await expect(log.getByText(text)).toHaveCount(0)

    // A second click and Enter while the first send is pending must not send it again.
    await page.getByRole('button', { name: 'Отправить' }).click({ force: true })
    await field.press('Enter')

    await fakeHub(page).releaseSends()
    await expect(log.getByText(text)).toHaveCount(1)
    await expect(field).toHaveValue('')
  })

  test('puts the text back and shows the error when the send fails', async ({ page }) => {
    const field = await openGeneralChat(page)
    const text = `не дошло ${Date.now()}`
    await fakeHub(page).failNextSends(['rate_limited'])

    await field.fill(text)
    await field.press('Enter')

    await expect(page.getByRole('alert')).toContainText('Слишком много сообщений')
    await expect(field).toHaveValue(text)
  })

  test('keeps what was typed while a send was failing, after the returned text', async ({
    page,
  }) => {
    const field = await openGeneralChat(page)
    await fakeHub(page).holdSends()
    await fakeHub(page).failNextSends(['unknown'])

    await field.fill('старое')
    await field.press('Enter')
    await expect(field).toHaveValue('')
    await field.pressSequentially('новое')

    await fakeHub(page).releaseSends()
    await expect(page.getByRole('alert')).toBeVisible()
    await expect(field).toHaveValue('старое\nновое')
  })
})
