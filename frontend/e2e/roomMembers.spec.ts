import { expect, test, type Locator, type Page } from '@playwright/test'

import { openChat, signIn } from './helpers'

/** Seeded: Anna owns it, and the demo user, Igor and Olga are members. Anna is online, Igor is not. */
const GENERAL_ROOM = 'Общий'
/** Seeded: Igor owns it, the demo user is a member. Anna has a chat with the demo user but is not in it. */
const DEV_ROOM = 'Разработка'
/** Seeded: the demo user is not a member; Anna, Igor and Petr are. Found through the room search. */
const CHESS_ROOM = 'Шахматы'
const ANNA = 'Анна Смирнова'
const IGOR = 'Игорь Петров'
const OLGA = 'Ольга Ким'
const PETR = 'Пётр Соколов'
const REQUEST_TEXT = 'Привет! Мы в одной комнате.'
const TITLE_HEADING_LEVEL = 1

function membersDialog(page: Page): Locator {
  return page.getByRole('dialog', { name: 'Участники' })
}

function memberRow(page: Page, name: string): Locator {
  return membersDialog(page).getByRole('listitem').filter({ hasText: name })
}

async function openMembers(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Участники', exact: true }).click()
  await expect(membersDialog(page)).toBeVisible()
}

test('the member list shows names, the owner, who is online, and no "write" button for oneself', async ({
  page,
}) => {
  await signIn(page)
  await openChat(page, GENERAL_ROOM)

  await openMembers(page)

  const dialog = membersDialog(page)
  await expect(dialog.getByRole('listitem')).toHaveCount(4)
  await expect(memberRow(page, ANNA)).toContainText('владелец')
  await expect(memberRow(page, ANNA)).toContainText('в сети')
  await expect(memberRow(page, IGOR)).toContainText('не в сети')
  await expect(memberRow(page, 'Вы (демо)')).toContainText('(вы)')
  await expect(memberRow(page, 'Вы (демо)').getByRole('button')).toHaveCount(0)
  await expect(dialog.getByText('@')).toHaveCount(0)
})

test('"Написать" opens the chat that already exists', async ({ page }) => {
  await signIn(page)
  await openChat(page, GENERAL_ROOM)
  await openMembers(page)

  await page.getByRole('button', { name: `Написать: ${ANNA}` }).click()

  await expect(page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: ANNA })).toBeVisible()
  await expect(membersDialog(page)).toHaveCount(0)
  await expect(page.getByLabel('Сообщение')).toBeEnabled()
})

test('"Написать" to someone who wrote first points to their request instead of sending one', async ({
  page,
}) => {
  await signIn(page)
  await openChat(page, GENERAL_ROOM)
  await openMembers(page)

  await page.getByRole('button', { name: `Написать: ${OLGA}` }).click()

  await expect(membersDialog(page).getByRole('status')).toHaveText(
    'Этот пользователь уже написал вам — примите запрос.',
  )
  await expect(page.getByLabel(`Первое сообщение для ${OLGA}`)).toHaveCount(0)
})

test('"Написать" without a chat sends a request, and a second try says it is waiting', async ({
  page,
}) => {
  await signIn(page)
  await page.getByRole('button', { name: 'Найти комнату' }).click()
  await page.getByRole('button', { name: `Вступить в комнату ${CHESS_ROOM}` }).click()
  await expect(
    page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: `# ${CHESS_ROOM}` }),
  ).toBeVisible()
  await expect(page.getByText('участников: 4')).toBeVisible()
  await openMembers(page)

  await page.getByRole('button', { name: `Написать: ${PETR}` }).click()
  await page.getByLabel(`Первое сообщение для ${PETR}`).fill(REQUEST_TEXT)
  await page.getByRole('button', { name: 'Отправить запрос' }).click()

  await expect(page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: PETR })).toBeVisible()
  await expect(page.getByText('Ожидает ответа', { exact: true })).toBeVisible()

  await page.getByRole('link', { name: 'К списку чатов' }).click()
  await openChat(page, CHESS_ROOM)
  await openMembers(page)
  await page.getByRole('button', { name: `Написать: ${PETR}` }).click()

  await expect(membersDialog(page).getByRole('status')).toHaveText(
    'Запрос уже отправлен — ждём ответа.',
  )
})

test('the member list closes with its close button', async ({ page }) => {
  await signIn(page)
  await openChat(page, GENERAL_ROOM)
  await openMembers(page)

  await membersDialog(page).getByRole('button', { name: 'Закрыть' }).click()

  await expect(membersDialog(page)).toHaveCount(0)
})

test('the invite list offers only chat partners, and marks the ones already in the room', async ({
  page,
}) => {
  await signIn(page)
  await openChat(page, DEV_ROOM)

  await page.getByRole('button', { name: 'Пригласить', exact: true }).click()

  const dialog = page.getByRole('dialog', { name: 'Пригласить в комнату' })
  await expect(dialog.getByRole('button', { name: `Пригласить: ${ANNA}` })).toBeVisible()
  await expect(dialog.getByRole('listitem').filter({ hasText: IGOR })).toContainText(
    'Уже в комнате',
  )
  await expect(dialog.getByText(OLGA)).toHaveCount(0)
  await expect(dialog.getByText(PETR)).toHaveCount(0)
})

test('inviting a chat partner adds them at once and the room shows "joined"', async ({ page }) => {
  await signIn(page)
  await openChat(page, DEV_ROOM)
  await expect(page.getByText('участников: 2')).toBeVisible()

  await page.getByRole('button', { name: 'Пригласить', exact: true }).click()
  const dialog = page.getByRole('dialog', { name: 'Пригласить в комнату' })
  await dialog.getByRole('button', { name: `Пригласить: ${ANNA}` }).click()
  await expect(dialog.getByRole('status')).toHaveText('Добавлен')
  await dialog.getByRole('button', { name: 'Закрыть' }).click()

  await expect(
    page.getByRole('log', { name: 'Сообщения' }).getByText(`${ANNA} присоединился`),
  ).toBeVisible()
  await expect(page.getByText('участников: 3')).toBeVisible()
  await openMembers(page)
  await expect(membersDialog(page).getByRole('listitem')).toHaveCount(3)
  await expect(memberRow(page, ANNA)).toContainText('в сети')
})

test('the "joined" line is not history: it does not touch the message pages', async ({ page }) => {
  await signIn(page)
  await openChat(page, DEV_ROOM)
  await page.getByRole('button', { name: 'Пригласить', exact: true }).click()
  const dialog = page.getByRole('dialog', { name: 'Пригласить в комнату' })
  await dialog.getByRole('button', { name: `Пригласить: ${ANNA}` }).click()
  await expect(dialog.getByRole('status')).toHaveText('Добавлен')
  await dialog.getByRole('button', { name: 'Закрыть' }).click()
  const log = page.getByRole('log', { name: 'Сообщения' })
  await expect(log.getByText(`${ANNA} присоединился`)).toBeVisible()

  const reply = 'Добро пожаловать!'
  await page.getByLabel('Сообщение').fill(reply)
  await page.getByRole('button', { name: 'Отправить' }).click()

  await expect(log.getByText(reply)).toBeVisible()
  const lines = await log.locator('p, article, li').allInnerTexts()
  const noticeAt = lines.findIndex((line) => line.includes(`${ANNA} присоединился`))
  const replyAt = lines.findIndex((line) => line.includes(reply))
  expect(noticeAt).toBeGreaterThanOrEqual(0)
  expect(replyAt).toBeGreaterThan(noticeAt)
})
