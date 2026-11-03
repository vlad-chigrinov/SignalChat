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

  // Saving is a download under the file name; the page itself stays where it is and no tab opens.
  const pageUrl = page.url()
  const [download] = await Promise.all([
    page.waitForEvent('download'),
    viewer.getByRole('button', { name: 'Скачать' }).click(),
  ])
  expect(download.suggestedFilename()).toBe('photo-1.png')
  expect(page.url()).toBe(pageUrl)
  expect(page.context().pages()).toHaveLength(1)
  await expect(viewer).toBeVisible()
  await expect(viewer.getByRole('button', { name: 'Перевернуть' })).toHaveText('')

  // There are no previous/next buttons: a swipe to the left shows the next photo, to the right the previous one.
  await expect(viewer.getByRole('button', { name: /Следующее|Предыдущее/ })).toHaveCount(0)
  const stage = viewer.getByTestId('photo-viewer-stage')
  const box = await stage.boundingBox()
  if (!box) throw new Error('The viewer stage has no box.')
  const { x, width } = box
  const middle = box.y + box.height / 2
  async function swipe(fromFraction: number, toFraction: number): Promise<void> {
    await page.mouse.move(x + width * fromFraction, middle)
    await page.mouse.down()
    await page.mouse.move(x + width * toFraction, middle, { steps: 8 })
    await page.mouse.up()
  }

  await swipe(0.7, 0.2)
  await expect(viewer.getByRole('status')).toHaveText('Фото 2 из 2')
  await expect(image).toHaveAttribute('data-rotation', '0') // a new photo starts upright
  await expect(viewer).toBeVisible() // the end of the swipe did not count as a click outside
  await swipe(0.2, 0.7)
  await expect(viewer.getByRole('status')).toHaveText('Фото 1 из 2')

  // The arrow keys still page through the photos.
  await page.keyboard.press('ArrowRight')
  await expect(viewer.getByRole('status')).toHaveText('Фото 2 из 2')

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

test('the limit message belongs to the pick that went over it: exactly nine photos show none', async ({
  page,
}) => {
  await signIn(page)
  await openChat(page, 'Общий')
  const alert = page.getByRole('alert').filter({ hasText: 'не больше 9 фото' })
  const previews = page.getByRole('list', { name: 'Выбранные фото' }).getByRole('img')
  const input = page.getByTestId('photo-input')

  await input.setInputFiles(await photos(page, MAX_PHOTOS))
  await expect(previews).toHaveCount(MAX_PHOTOS)
  await expect(alert).toHaveCount(0)

  await page.getByRole('button', { name: 'Убрать фото 1' }).click()
  await input.setInputFiles(await photos(page, 2))
  await expect(alert).toBeVisible()
  await expect(previews).toHaveCount(MAX_PHOTOS)

  // Sending clears the photos, and the message about them does not stay behind.
  await page.getByRole('button', { name: 'Отправить' }).click()
  await expect(previews).toHaveCount(0)
  await expect(alert).toHaveCount(0)

  await input.setInputFiles(await photos(page, MAX_PHOTOS))
  await expect(previews).toHaveCount(MAX_PHOTOS)
  await expect(alert).toHaveCount(0)
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
