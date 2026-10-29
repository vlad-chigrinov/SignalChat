import { expect, type Page } from '@playwright/test'

const TITLE_HEADING_LEVEL = 1

export function roomTitle(name: string): string {
  return `# ${name}`
}

export async function createRoom(page: Page, name: string): Promise<void> {
  await page.getByRole('button', { name: 'Создать комнату' }).click()
  await page.getByLabel('Название', { exact: true }).fill(name)
  await page.getByRole('button', { name: 'Создать', exact: true }).click()
  await expect(
    page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: roomTitle(name) }),
  ).toBeVisible()
}

export async function joinRoomFromCatalog(page: Page, name: string): Promise<void> {
  await page.getByRole('link', { name: 'Найти комнату' }).click()
  await page.getByLabel('Название комнаты').fill(name)
  await page.getByRole('button', { name: `Вступить в комнату ${name}` }).click()
  await expect(
    page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: roomTitle(name) }),
  ).toBeVisible()
}

export function messageLog(page: Page) {
  return page.getByRole('log', { name: 'Сообщения' })
}

export async function sendMessage(page: Page, text: string): Promise<void> {
  await page.getByLabel('Сообщение').fill(text)
  await page.getByRole('button', { name: 'Отправить' }).click()
  await expect(messageLog(page).getByText(text, { exact: true })).toBeVisible()
}

/** Types without sending: the composer reports typing while the text changes. */
export async function typeWithoutSending(page: Page, text: string): Promise<void> {
  await page.getByLabel('Сообщение').pressSequentially(text)
}

export function typingLine(page: Page) {
  return page.getByText(/печатает…/)
}

/** The banner is the only status that says the messages are not sent or the link is down. */
export function connectionBanner(page: Page) {
  return page.getByRole('status').filter({ hasText: /Переподключение…|Сообщения не отправляются/ })
}
