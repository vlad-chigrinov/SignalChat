import { expect, test } from '@playwright/test'

import {
  awaitingAnswerStatus,
  awaitingAnswerSubtitle,
  messageLog,
  requestItem,
  requestsSection,
  sendFirstMessage,
  sendMessage,
} from './ui'
import { startUser } from './users'

const FIRST_REQUEST_TEXT = 'Привет! Можно написать тебе про релиз?'
const DECLINED_REQUEST_TEXT = 'Есть минутка для вопроса?'
const REPEATED_REQUEST_TEXT = 'Ещё раз, пожалуйста'
const HTML_REQUEST_TEXT = '<script>window.__xss = true</script>'
const ALREADY_SENT_TEXT = 'Запрос уже отправлен — ждём ответа.'
const ACCEPTED_REPLY = 'Принял, пишу'
const PEER_REPLY = 'Отлично, давай'
const TITLE_HEADING_LEVEL = 1
const ONLINE_LABEL = 'в сети'
const BACK_TO_LIST_LABEL = 'К списку чатов'

test('a request reaches the recipient live; accepting opens the chat for both, live', async ({
  browser,
}) => {
  const alice = await startUser(browser, 'alice')
  const bob = await startUser(browser, 'bob')
  try {
    await sendFirstMessage(alice.page, bob.handle, FIRST_REQUEST_TEXT)
    await expect(
      alice.page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: bob.handle }),
    ).toBeVisible()
    await expect(awaitingAnswerSubtitle(alice.page)).toBeVisible()
    await expect(awaitingAnswerStatus(alice.page)).toBeVisible()
    await expect(alice.page.getByLabel('Сообщение')).toBeDisabled()

    // Bob never reloads: the request arrives through DirectRequestReceived.
    await expect(requestsSection(bob.page)).toContainText(FIRST_REQUEST_TEXT)
    await requestItem(bob.page, FIRST_REQUEST_TEXT).getByRole('button', { name: 'Принять' }).click()
    await expect(
      bob.page.getByRole('heading', { level: TITLE_HEADING_LEVEL, name: alice.handle }),
    ).toBeVisible()
    await expect(bob.page.getByLabel('Сообщение')).toBeEnabled()

    // Alice never reloads: the acceptance arrives through DirectRequestAccepted.
    await expect(alice.page.getByLabel('Сообщение')).toBeEnabled()
    await expect(awaitingAnswerStatus(alice.page)).toBeHidden()

    await sendMessage(alice.page, ACCEPTED_REPLY)
    await expect(messageLog(bob.page).getByText(ACCEPTED_REPLY, { exact: true })).toBeVisible()
    await sendMessage(bob.page, PEER_REPLY)
    await expect(messageLog(alice.page).getByText(PEER_REPLY, { exact: true })).toBeVisible()

    // Presence is a snapshot taken on connect: reloading picks up Bob, who shares this chat now.
    await alice.page.reload()
    await expect(alice.page.getByText(ONLINE_LABEL, { exact: true })).toBeVisible()
  } finally {
    await alice.context.close()
    await bob.context.close()
  }
})

test('a declined request stays waiting for the writer, and a repeat says so', async ({
  browser,
}) => {
  const alice = await startUser(browser, 'alice')
  const carol = await startUser(browser, 'carol')
  try {
    await sendFirstMessage(alice.page, carol.handle, DECLINED_REQUEST_TEXT)
    await expect(awaitingAnswerSubtitle(alice.page)).toBeVisible()

    await expect(requestItem(carol.page, DECLINED_REQUEST_TEXT)).toBeVisible()
    await requestItem(carol.page, DECLINED_REQUEST_TEXT)
      .getByRole('button', { name: 'Отклонить' })
      .click()
    await expect(requestItem(carol.page, DECLINED_REQUEST_TEXT)).toHaveCount(0)

    // A decline is silent: the writer is not told, and the chat still waits for an answer.
    await expect(awaitingAnswerSubtitle(alice.page)).toBeVisible()
    await expect(alice.page.getByLabel('Сообщение')).toBeDisabled()

    await alice.page.getByRole('link', { name: BACK_TO_LIST_LABEL }).click()
    await sendFirstMessage(alice.page, carol.handle, REPEATED_REQUEST_TEXT)
    await expect(alice.page.getByRole('alert')).toHaveText(ALREADY_SENT_TEXT)
  } finally {
    await alice.context.close()
    await carol.context.close()
  }
})

test('a request text with HTML characters is shown to the recipient as plain text', async ({
  browser,
}) => {
  const alice = await startUser(browser, 'alice')
  const erin = await startUser(browser, 'erin')
  try {
    await sendFirstMessage(alice.page, erin.handle, HTML_REQUEST_TEXT)

    await expect(
      requestsSection(erin.page).getByText(HTML_REQUEST_TEXT, { exact: true }),
    ).toBeVisible()
    await expect(erin.page.locator('main script')).toHaveCount(0)
    const injected = await erin.page.evaluate(
      () => (window as unknown as { __xss?: boolean }).__xss === true,
    )
    expect(injected).toBe(false)
  } finally {
    await alice.context.close()
    await erin.context.close()
  }
})
