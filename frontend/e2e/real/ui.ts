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
  await page.getByRole('button', { name: 'Найти комнату' }).click()
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

/** Opens the search dialog, picks `peerName` and writes the first message: a request. */
export async function sendFirstMessage(page: Page, peerName: string, text: string): Promise<void> {
  await page.getByRole('button', { name: 'Новое сообщение' }).click()
  await page.getByLabel('Имя собеседника').fill(peerName)
  await page.getByRole('button', { name: `Написать: ${peerName}` }).click()
  await page.getByLabel(`Первое сообщение для ${peerName}`).fill(text)
  await page.getByRole('button', { name: 'Отправить запрос' }).click()
}

/** The Requests section of the chat list: incoming requests live here. */
export function requestsSection(page: Page) {
  return page.getByRole('region', { name: 'Запросы' })
}

/** One incoming request, found by its text. */
export function requestItem(page: Page, text: string) {
  return page.getByRole('listitem').filter({ hasText: text })
}

/** The composer's reason while a request waits for the peer's answer. */
export function awaitingAnswerStatus(page: Page) {
  return page.getByRole('status').filter({ hasText: 'Ожидает ответа собеседника.' })
}

/** The subtitle of a chat the user wrote and the peer has not answered yet. */
export function awaitingAnswerSubtitle(page: Page) {
  return page.getByText('Ожидает ответа', { exact: true })
}
