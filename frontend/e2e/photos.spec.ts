import { expect, test, type Page } from '@playwright/test'

import { openChat, signIn } from './helpers'

const MAX_PHOTOS = 9
const UPLOAD_COUNTER = '__mockPhotoUploads'
const PHOTO_SIZE = { width: 40, height: 30 }

interface PickedFile {
  name: string
  mimeType: string
  buffer: Buffer
}

/** A real PNG, drawn by the browser, so the previews and thumbnails have something to decode. */
async function photo(page: Page, name: string, color: string): Promise<PickedFile> {
  const base64 = await page.evaluate(
    ({ width, height, fill }) => {
      const canvas = document.createElement('canvas')
      canvas.width = width
      canvas.height = height
      const context = canvas.getContext('2d')
      if (context) {
        context.fillStyle = fill
        context.fillRect(0, 0, width, height)
      }
      return canvas.toDataURL('image/png').split(',')[1] ?? ''
    },
    { ...PHOTO_SIZE, fill: color },
  )
  return { name, mimeType: 'image/png', buffer: Buffer.from(base64, 'base64') }
}

async function photos(page: Page, count: number): Promise<PickedFile[]> {
  const files: PickedFile[] = []
  for (let index = 0; index < count; index += 1) {
    files.push(await photo(page, `photo-${index + 1}.png`, `hsl(${index * 40} 70% 50%)`))
  }
  return files
}

function uploads(page: Page): Promise<number> {
  return page.evaluate(
    (key) => (window as unknown as Record<string, number | undefined>)[key] ?? 0,
    UPLOAD_COUNTER,
  )
}

test('pick photos, remove one, nothing is uploaded until send, then the grid, the viewer and rotation', async ({
  page,
}) => {
  await signIn(page)
  await openChat(page, 'Общий')

  await page.getByTestId('photo-input').setInputFiles(await photos(page, 3))
  const previews = page.getByRole('list', { name: 'Выбранные фото' })
  await expect(previews.getByRole('img')).toHaveCount(3)

  await page.getByRole('button', { name: 'Убрать фото 2' }).click()
  await expect(previews.getByRole('img')).toHaveCount(2)
  expect(await uploads(page)).toBe(0)

  const caption = `подпись ${Date.now()}`
  await page.getByLabel('Сообщение').fill(caption)
  await page.getByRole('button', { name: 'Отправить' }).click()

  const log = page.getByRole('log', { name: 'Сообщения' })
  await expect(log.getByText(caption)).toBeVisible()
  expect(await uploads(page)).toBe(1)
  await expect(previews).toHaveCount(0)

  const grid = log.getByRole('list', { name: 'Фото: 2' })
  await expect(grid.getByRole('button')).toHaveCount(2)

  await grid.getByRole('button', { name: 'Открыть фото 1 из 2' }).click()
  const viewer = page.getByRole('dialog', { name: 'Просмотр фото' })
  await expect(viewer).toBeVisible()
  const image = viewer.getByTestId('photo-viewer-image')
  await expect(image).toHaveAttribute('data-rotation', '0')

  await viewer.getByRole('button', { name: 'Перевернуть' }).click()
  await expect(image).toHaveAttribute('data-rotation', '90')
  await viewer.getByRole('button', { name: 'Перевернуть' }).click()
  await expect(image).toHaveAttribute('data-rotation', '180')

  await expect(viewer.getByRole('link', { name: 'Скачать' })).toHaveAttribute(
    'download',
    'photo-1.png',
  )

  // The arrow keys page through the photos; a new photo starts upright.
  await page.keyboard.press('ArrowRight')
  await expect(viewer.getByRole('status')).toHaveText('Фото 2 из 2')
  await expect(image).toHaveAttribute('data-rotation', '0')

  await page.keyboard.press('Escape')
  await expect(viewer).toHaveCount(0)
})

test('more than nine photos are refused with a message, and nine stay', async ({ page }) => {
  await signIn(page)
  await openChat(page, 'Общий')

  await page.getByTestId('photo-input').setInputFiles(await photos(page, MAX_PHOTOS + 2))
  await expect(page.getByRole('alert').filter({ hasText: 'не больше 9 фото' })).toBeVisible()
  await expect(page.getByRole('list', { name: 'Выбранные фото' }).getByRole('img')).toHaveCount(
    MAX_PHOTOS,
  )
  await expect(page.getByRole('button', { name: 'Прикрепить фото' })).toBeDisabled()
  expect(await uploads(page)).toBe(0)
})

test('a photo without a caption can be sent, and the viewer closes on Close and on a click outside', async ({
  page,
}) => {
  await signIn(page)
  await openChat(page, 'Общий')

  await page.getByTestId('photo-input').setInputFiles(await photos(page, 1))
  await page.getByRole('button', { name: 'Отправить' }).click()

  const log = page.getByRole('log', { name: 'Сообщения' })
  const open = log.getByRole('button', { name: 'Открыть фото 1 из 1' }).last()
  await expect(open).toBeVisible()

  await open.click()
  const viewer = page.getByRole('dialog', { name: 'Просмотр фото' })
  await expect(viewer).toBeVisible()
  await viewer.getByRole('button', { name: 'Закрыть' }).click()
  await expect(viewer).toHaveCount(0)

  await open.click()
  await expect(viewer).toBeVisible()
  await viewer.click({ position: { x: 5, y: 300 } })
  await expect(viewer).toHaveCount(0)
})
