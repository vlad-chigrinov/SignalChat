import { expect, test, type Page } from '@playwright/test'

import { signIn } from './helpers'

// A 1x1 PNG: enough for the mock, which keeps the file and shows it.
const TINY_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
)

async function openProfile(page: Page): Promise<void> {
  await signIn(page)
  await page.getByRole('link', { name: 'Профиль' }).click()
  await expect(page.getByRole('heading', { name: 'Вы (демо)' })).toBeVisible()
}

test('the user renames themselves and the new name shows at once', async ({ page }) => {
  await openProfile(page)

  await page.getByLabel('Имя', { exact: true }).fill('  Новое имя  ')
  await page.getByRole('button', { name: 'Сохранить имя' }).click()

  await expect(page.getByRole('heading', { name: 'Новое имя' })).toBeVisible()
  await expect(page.getByRole('status')).toHaveText('Имя сохранено.')
  await expect(page.getByLabel('Имя', { exact: true })).toHaveValue('Новое имя')
  await expect(page.getByRole('button', { name: 'Сохранить имя' })).toBeDisabled()
})

test('the new name is kept when the user leaves the page and comes back', async ({ page }) => {
  await openProfile(page)
  await page.getByLabel('Имя', { exact: true }).fill('Автор Новый')
  await page.getByRole('button', { name: 'Сохранить имя' }).click()
  await expect(page.getByRole('heading', { name: 'Автор Новый' })).toBeVisible()

  await page.getByRole('link', { name: 'Чаты' }).click()
  await expect(page.getByRole('heading', { name: 'Комнаты' })).toBeVisible()
  await page.getByRole('link', { name: 'Профиль' }).click()

  await expect(page.getByRole('heading', { name: 'Автор Новый' })).toBeVisible()
  await expect(page.getByLabel('Имя', { exact: true })).toHaveValue('Автор Новый')
})

test('an empty name is refused with a message and nothing is saved', async ({ page }) => {
  await openProfile(page)

  await page.getByLabel('Имя', { exact: true }).fill('   ')
  await page.getByRole('button', { name: 'Сохранить имя' }).click()

  await expect(page.getByRole('alert')).toHaveText('Введите имя.')
  await expect(page.getByRole('heading', { name: 'Вы (демо)' })).toBeVisible()
})

test('a picture becomes the avatar and can be replaced by the Google one', async ({ page }) => {
  await openProfile(page)
  await expect(page.getByRole('main').locator('img')).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Фото из Google' })).toHaveCount(0)

  await page.getByLabel('Файл с фото').setInputFiles({
    name: 'me.png',
    mimeType: 'image/png',
    buffer: TINY_PNG,
  })

  await expect(page.getByRole('main').locator('img')).toHaveCount(1)
  await page.getByRole('button', { name: 'Фото из Google' }).click()
  await expect(page.getByRole('main').locator('img')).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Фото из Google' })).toHaveCount(0)
})

test('a file that is not a supported picture is refused before it is sent', async ({ page }) => {
  await openProfile(page)

  await page.getByLabel('Файл с фото').setInputFiles({
    name: 'logo.svg',
    mimeType: 'image/svg+xml',
    buffer: Buffer.from('<svg xmlns="http://www.w3.org/2000/svg"></svg>'),
  })

  await expect(page.getByRole('alert')).toContainText('JPEG, PNG, GIF или WebP')
  await expect(page.getByRole('main').locator('img')).toHaveCount(0)
})
