import { expect, test } from '@playwright/test'

import { signIn } from './helpers'

test('route guard sends an anonymous visitor to /login and remembers the target', async ({
  page,
}) => {
  await page.goto('/chat/room-dev')
  await expect(page).toHaveURL(/\/login\?redirect=(%2F|\/)chat(%2F|\/)room-dev$/)
  await expect(page.getByRole('button', { name: 'Войти через Google' })).toBeVisible()

  await page.getByRole('button', { name: 'Войти через Google' }).click()
  await expect(page).toHaveURL('/chat/room-dev')
  await expect(page.getByRole('heading', { level: 1, name: /Разработка/ })).toBeVisible()
})

test('guard also protects the profile page', async ({ page }) => {
  await page.goto('/profile')
  await expect(page).toHaveURL(/\/login/)
})

test('logout clears the session and the guard blocks protected pages again', async ({ page }) => {
  await signIn(page)
  await page.getByRole('link', { name: 'Профиль' }).click()
  await expect(page.getByRole('heading', { name: 'Вы (демо)' })).toBeVisible()

  await page.getByRole('button', { name: 'Выйти' }).click()
  await expect(page).toHaveURL(/\/login/)

  // Client-side navigation back to a protected route must not show the old state.
  await page.goBack()
  await expect(page).toHaveURL(/\/login/)
  await expect(page.getByRole('heading', { name: 'Комнаты' })).toHaveCount(0)
})

test('no tokens are kept in web storage', async ({ page }) => {
  await signIn(page)
  const storage = await page.evaluate(() => ({
    local: Object.keys(window.localStorage),
    session: Object.keys(window.sessionStorage),
  }))
  // Vue devtools (dev server only) keeps its own UI settings; they are not app data.
  const appKeys = (keys: string[]) =>
    keys.filter((key) => !key.includes('vue-devtools') && !key.includes('VUE_DEVTOOLS'))
  expect(appKeys(storage.local)).toEqual([])
  expect(appKeys(storage.session)).toEqual([])
})
