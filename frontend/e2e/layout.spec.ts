import { expect, test, type Page } from '@playwright/test'

import { openChat, signIn } from './helpers'

const GENERAL_ROOM = 'Общий'
const ANNA = 'Анна Смирнова'
const HEIGHT = 800
/** Just below and just at the widths where the sidebar (720) and the participants (1080) appear. */
const NARROW = { width: 719, height: HEIGHT }
const WITH_SIDEBAR = { width: 720, height: HEIGHT }
const BELOW_PARTICIPANTS = { width: 1079, height: HEIGHT }
const WITH_PARTICIPANTS = { width: 1080, height: HEIGHT }

const chatList = (page: Page) => page.getByRole('navigation', { name: 'Список чатов' })
const participants = (page: Page) => page.getByTestId('participants-panel')

test.describe('narrow screen', () => {
  test.use({ viewport: NARROW })

  test('the chat list and the chat take turns', async ({ page }) => {
    await signIn(page)
    await expect(chatList(page)).toBeVisible()

    await openChat(page, GENERAL_ROOM)
    await expect(chatList(page)).toHaveCount(0)
    await expect(participants(page)).toHaveCount(0)

    await page.getByRole('link', { name: 'К списку чатов' }).click()
    await expect(chatList(page)).toBeVisible()
  })
})

test.describe('from 720px', () => {
  test.use({ viewport: WITH_SIDEBAR })

  test('the chat list stays on the left and the chat fills the rest', async ({ page }) => {
    await signIn(page)
    await expect(page.getByText('Выберите чат слева')).toBeVisible()

    await openChat(page, GENERAL_ROOM)
    await expect(chatList(page)).toBeVisible()
    await expect(page.getByRole('link', { name: 'К списку чатов' })).toHaveCount(0)
    await expect(participants(page)).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Участники', exact: true })).toBeVisible()

    const list = await chatList(page).boundingBox()
    const chat = await page.getByRole('main').boundingBox()
    expect(list).not.toBeNull()
    expect(chat).not.toBeNull()
    if (!list || !chat) return
    expect(chat.x).toBeGreaterThanOrEqual(list.x + list.width)
    expect(chat.x + chat.width).toBeCloseTo(WITH_SIDEBAR.width, 0)
  })

  test('another chat opens from the list without leaving the screen', async ({ page }) => {
    await signIn(page)
    await openChat(page, GENERAL_ROOM)
    await openChat(page, ANNA)
    await expect(chatList(page)).toBeVisible()
  })
})

test.describe('below 1080px', () => {
  test.use({ viewport: BELOW_PARTICIPANTS })

  test('members still open in a dialog', async ({ page }) => {
    await signIn(page)
    await openChat(page, GENERAL_ROOM)
    await expect(participants(page)).toHaveCount(0)
    await page.getByRole('button', { name: 'Участники', exact: true }).click()
    await expect(page.getByRole('dialog', { name: 'Участники' })).toBeVisible()
  })
})

test.describe('from 1080px', () => {
  test.use({ viewport: WITH_PARTICIPANTS })

  test('a room shows its participants on the right, next to the chat list', async ({ page }) => {
    await signIn(page)
    await openChat(page, GENERAL_ROOM)
    await expect(chatList(page)).toBeVisible()
    await expect(participants(page)).toBeVisible()
    await expect(participants(page).getByText(ANNA)).toBeVisible()
    await expect(page.getByRole('button', { name: 'Участники', exact: true })).toHaveCount(0)
  })

  test('a direct chat has no participants panel', async ({ page }) => {
    await signIn(page)
    await openChat(page, ANNA)
    await expect(chatList(page)).toBeVisible()
    await expect(participants(page)).toHaveCount(0)
  })

  test('"Написать" in the panel opens the chat with that person', async ({ page }) => {
    await signIn(page)
    await openChat(page, GENERAL_ROOM)
    await participants(page)
      .getByRole('button', { name: `Написать: ${ANNA}` })
      .click()
    await expect(page.getByRole('heading', { level: 1, name: ANNA })).toBeVisible()
  })
})
