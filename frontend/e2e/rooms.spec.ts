import { expect, test } from '@playwright/test'

import { openChat, signIn } from './helpers'

/** Seeded in the mock: taken by 'Общий' (case-insensitive). */
const TAKEN_ROOM_NAME = 'общий'
/** Seeded in the mock: a room the demo user is not in, found through discovery. */
const CATALOG_ROOM = 'Шахматы'
/** Has no chat with the demo user, so the first message to them is a request. */
const SEARCHED_USER = 'Пётр Соколов'
const REQUEST_TEXT = 'Привет! Есть минутка?'
const ROOM_NAME_WITH_HTML = '<script>alert(1)</script>'
const TITLE_HEADING_LEVEL = 1

test('creates a room and opens it', async ({ page }) => {
  await signIn(page)
  const roomName = `e2e комната ${Date.now()}`

  await page.getByRole('button', { name: 'Создать комнату' }).click()
  await page.getByLabel('Название', { exact: true }).fill(roomName)
  await page.getByRole('button', { name: 'Создать', exact: true }).click()

  await expect(
    page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: `# ${roomName}` }),
  ).toBeVisible()
})

test('a taken room name is reported at the name field', async ({ page }) => {
  await signIn(page)

  await page.getByRole('button', { name: 'Создать комнату' }).click()
  await page.getByLabel('Название', { exact: true }).fill(TAKEN_ROOM_NAME)
  await page.getByRole('button', { name: 'Создать', exact: true }).click()

  await expect(page.getByText('Комната с таким названием уже есть.')).toBeVisible()
  await expect(page.getByLabel('Название', { exact: true })).toHaveAttribute('aria-invalid', 'true')
})

test('the create dialog takes focus and closes on Escape', async ({ page }) => {
  await signIn(page)

  await page.getByRole('button', { name: 'Создать комнату' }).click()
  await expect(page.getByRole('dialog')).toBeVisible()
  await expect(page.getByLabel('Название', { exact: true })).toBeFocused()

  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).toHaveCount(0)
})

test('a dialog closes with its close button', async ({ page }) => {
  await signIn(page)

  await page.getByRole('button', { name: 'Создать комнату' }).click()
  await expect(page.getByRole('dialog')).toBeVisible()

  await page.getByRole('dialog').getByRole('button', { name: 'Закрыть' }).click()
  await expect(page.getByRole('dialog')).toHaveCount(0)
})

test('a dialog closes on a click outside it, but not when a press began inside it', async ({
  page,
}) => {
  await signIn(page)

  await page.getByRole('button', { name: 'Создать комнату' }).click()
  const title = page.getByRole('heading', { name: 'Новая комната' })

  // Selecting text in a field and letting go over the backdrop must keep the dialog.
  const box = await title.boundingBox()
  await page.mouse.move(box!.x + 2, box!.y + 2)
  await page.mouse.down()
  await page.mouse.move(2, 2)
  await page.mouse.up()
  await expect(page.getByRole('dialog')).toBeVisible()

  await page.mouse.click(2, 2)
  await expect(page.getByRole('dialog')).toHaveCount(0)
})

test('room search is a dialog that closes with its close button', async ({ page }) => {
  await signIn(page)

  await page.getByRole('button', { name: 'Найти комнату' }).click()
  const dialog = page.getByRole('dialog', { name: 'Найти комнату' })
  await expect(dialog).toBeVisible()
  await expect(page).toHaveURL(/\/$/)

  await dialog.getByRole('button', { name: 'Закрыть' }).click()
  await expect(dialog).toHaveCount(0)
})

test('discovers a room, joins it and leaves it again', async ({ page }) => {
  await signIn(page)

  await page.getByRole('button', { name: 'Найти комнату' }).click()
  await page.getByLabel('Название комнаты').fill('шах')
  await page.getByRole('button', { name: `Вступить в комнату ${CATALOG_ROOM}` }).click()

  await expect(
    page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: `# ${CATALOG_ROOM}` }),
  ).toBeVisible()

  await page.getByRole('button', { name: 'Покинуть комнату' }).click()
  await expect(page.getByRole('heading', { name: 'Комнаты' })).toBeVisible()
  await expect(page.getByRole('link', { name: new RegExp(CATALOG_ROOM) })).toHaveCount(0)
})

test('finds a user, writes the first message as a request, without showing an email', async ({
  page,
}) => {
  await signIn(page)

  await page.getByRole('button', { name: 'Новое сообщение' }).click()
  await page.getByLabel('Имя собеседника').fill('Пётр')
  const dialog = page.getByRole('dialog')
  await expect(dialog).not.toContainText('@')

  await page.getByRole('button', { name: `Написать: ${SEARCHED_USER}` }).click()
  await page.getByLabel(`Первое сообщение для ${SEARCHED_USER}`).fill(REQUEST_TEXT)
  await page.getByRole('button', { name: 'Отправить запрос' }).click()
  await expect(
    page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: SEARCHED_USER }),
  ).toBeVisible()
})

test('a room name with HTML characters is shown as text', async ({ page }) => {
  await signIn(page)

  await page.getByRole('button', { name: 'Создать комнату' }).click()
  await page.getByLabel('Название', { exact: true }).fill(ROOM_NAME_WITH_HTML)
  await page.getByRole('button', { name: 'Создать', exact: true }).click()

  const title = page.getByRole('heading', { level: TITLE_HEADING_LEVEL })
  await expect(title).toHaveText(`# ${ROOM_NAME_WITH_HTML}`)
  await expect(page.locator('main script')).toHaveCount(0)
})

test('a room chat offers to leave the room', async ({ page }) => {
  await signIn(page)
  await openChat(page, 'Общий')

  await expect(page.getByRole('button', { name: 'Покинуть комнату' })).toBeVisible()
})
