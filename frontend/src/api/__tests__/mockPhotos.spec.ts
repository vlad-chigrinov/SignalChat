import { describe, expect, it } from 'vitest'

import { MAX_PHOTOS_PER_MESSAGE, PHOTO_MAX_FILE_BYTES } from '../limits'
import { ApiError } from '../http'
import { createSeedStore, mockCurrentUser } from '../mock/mockData'
import { historyOf, previewOf } from '../mock/mockHistories'
import { sendPhotoMessage } from '../mock/mockPhotos'

const ROOM = 'room-general'
const PENDING_DIRECT = 'dm-request-olga'

function picture(name = 'a.png', type = 'image/png'): File {
  return new File([new Uint8Array([1, 2, 3])], name, { type })
}

function pictures(count: number): File[] {
  return Array.from({ length: count }, (_, index) => picture(`p${index}.png`))
}

async function codeOf(action: Promise<unknown>): Promise<string | null> {
  try {
    await action
  } catch (error) {
    return error instanceof ApiError ? error.code : 'other'
  }
  return null
}

describe('mock photo messages (same rules as the server)', () => {
  it('stores a message with its photos in the history and in the list preview', async () => {
    const store = createSeedStore()

    const message = await sendPhotoMessage(store, mockCurrentUser.id, ROOM, '  закат ', pictures(3))

    expect(message.text).toBe('закат')
    expect(message.attachments.map((photo) => photo.fileName)).toEqual([
      'p0.png',
      'p1.png',
      'p2.png',
    ])
    expect(historyOf(store, ROOM).at(-1)).toBe(message)
    expect(previewOf(store, ROOM)).toMatchObject({ text: 'закат', photoCount: 3 })
  })

  it('takes a message without a caption', async () => {
    const message = await sendPhotoMessage(createSeedStore(), mockCurrentUser.id, ROOM, '', [
      picture(),
    ])

    expect(message.text).toBe('')
    expect(message.attachments).toHaveLength(1)
  })

  it('refuses no photo and more than nine with invalid_attachments', async () => {
    const store = createSeedStore()

    expect(await codeOf(sendPhotoMessage(store, mockCurrentUser.id, ROOM, '', []))).toBe(
      'invalid_attachments',
    )
    expect(
      await codeOf(
        sendPhotoMessage(store, mockCurrentUser.id, ROOM, '', pictures(MAX_PHOTOS_PER_MESSAGE + 1)),
      ),
    ).toBe('invalid_attachments')
  })

  it('refuses a file that is not a picture with invalid_image', async () => {
    const code = await codeOf(
      sendPhotoMessage(createSeedStore(), mockCurrentUser.id, ROOM, '', [
        picture('a.pdf', 'application/pdf'),
      ]),
    )

    expect(code).toBe('invalid_image')
  })

  it('refuses a file over the limit with attachment_too_large', async () => {
    const large = new File([new Uint8Array(PHOTO_MAX_FILE_BYTES + 1)], 'big.png', {
      type: 'image/png',
    })

    expect(
      await codeOf(sendPhotoMessage(createSeedStore(), mockCurrentUser.id, ROOM, '', [large])),
    ).toBe('attachment_too_large')
  })

  it('answers not_found to a user who is not a member, and for a request not accepted yet', async () => {
    const store = createSeedStore()

    expect(await codeOf(sendPhotoMessage(store, 'someone-else', ROOM, '', [picture()]))).toBe(
      'not_found',
    )
    expect(
      await codeOf(sendPhotoMessage(store, mockCurrentUser.id, PENDING_DIRECT, '', [picture()])),
    ).toBe('not_found')
  })

  it('leaves the history alone when a photo is refused', async () => {
    const store = createSeedStore()
    const before = historyOf(store, ROOM).length

    await codeOf(sendPhotoMessage(store, mockCurrentUser.id, ROOM, '', pictures(10)))

    expect(historyOf(store, ROOM)).toHaveLength(before)
  })
})
