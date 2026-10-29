import { expect, test, type Page } from '@playwright/test'

import { signIn } from './helpers'

/** Has no chat with the demo user: the first message to them is a request. */
const NEW_PEER = 'Пётр Соколов'
/** Wrote to the demo user first (seeded): a request waiting for an answer. */
const REQUESTER = 'Ольга Ким'
const REQUESTER_TEXT = 'Здравствуйте! Можно обсудить релиз?'
const REQUEST_TEXT = 'Привет! Есть минутка?'
const TITLE_HEADING_LEVEL = 1
const WAITING_TEXT = 'Ожидает ответа собеседника.'
const ALREADY_SENT_TEXT = 'Запрос уже отправлен — ждём ответа.'

/** Opens the search dialog and writes the first message to the user found by `name`. */
async function sendRequestTo(page: Page, name: string, text: string): Promise<void> {
  await page.getByRole('button', { name: 'Новое сообщение' }).click()
  await page.getByLabel('Имя собеседника').fill(name)
  await page.getByRole('button', { name: `Написать: ${name}` }).click()
  await page.getByLabel(`Первое сообщение для ${name}`).fill(text)
  await page.getByRole('button', { name: 'Отправить запрос' }).click()
}

test('a first message is a request: it waits for an answer, and a repeat says so', async ({
  page,
}) => {
  await signIn(page)

  await sendRequestTo(page, 'Пётр', REQUEST_TEXT)

  await expect(
    page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: NEW_PEER }),
  ).toBeVisible()
  await expect(page.getByText('Ожидает ответа', { exact: true })).toBeVisible()
  await expect(page.getByRole('status').filter({ hasText: WAITING_TEXT })).toBeVisible()
  await expect(page.getByLabel('Сообщение')).toBeDisabled()

  await page.getByRole('link', { name: 'К списку чатов' }).click()
  await sendRequestTo(page, 'Пётр', REQUEST_TEXT)
  await expect(page.getByRole('alert')).toHaveText(ALREADY_SENT_TEXT)
})

test('an incoming request is answered in the list: accepting opens the chat for writing', async ({
  page,
}) => {
  await signIn(page)

  const requests = page.getByRole('region', { name: 'Запросы' })
  const request = page.getByRole('listitem').filter({ hasText: REQUESTER_TEXT })
  await expect(request).toBeVisible()
  await expect(request.getByText(REQUESTER, { exact: true })).toBeVisible()
  await expect(requests).toContainText(REQUESTER_TEXT)

  await request.getByRole('button', { name: 'Принять' }).click()
  await expect(
    page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: REQUESTER }),
  ).toBeVisible()
  await expect(page.getByLabel('Сообщение')).toBeEnabled()

  const reply = 'Да, давай в четверг'
  await page.getByLabel('Сообщение').fill(reply)
  await page.getByRole('button', { name: 'Отправить' }).click()
  await expect(page.getByRole('log', { name: 'Сообщения' }).getByText(reply)).toBeVisible()
})

test('declining a request removes it from the list without a trace for the writer', async ({
  page,
}) => {
  await signIn(page)

  const request = page.getByRole('listitem').filter({ hasText: REQUESTER_TEXT })
  await request.getByRole('button', { name: 'Отклонить' }).click()

  await expect(page.getByText(REQUESTER_TEXT)).toHaveCount(0)
  await expect(page.getByText('Новых запросов нет.')).toBeVisible()
})
