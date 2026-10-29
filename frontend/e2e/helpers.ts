import { expect, type Page } from '@playwright/test'

/** Mock-mode sign-in: the button works without Google. */
export async function signIn(page: Page, path = '/login'): Promise<void> {
  await page.goto(path)
  await page.getByRole('button', { name: 'Войти через Google' }).click()
  await expect(page.getByRole('heading', { name: 'Комнаты' })).toBeVisible()
}

export async function openChat(page: Page, title: string): Promise<void> {
  await page
    .getByRole('link', { name: new RegExp(title) })
    .first()
    .click()
  await expect(page.getByRole('heading', { level: 1, name: new RegExp(title) })).toBeVisible()
}
