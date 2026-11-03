import { describe, expect, it } from 'vitest'

import { MAX_PHOTOS_PER_MESSAGE, PHOTO_MAX_FILE_BYTES } from '@/api/limits'

import { addPhotos, removePhoto } from '../photoSelection'

function photo(name: string, type = 'image/jpeg', size = 1024): File {
  return new File([new Uint8Array(size)], name, { type })
}

function photos(count: number, prefix = 'p'): File[] {
  return Array.from({ length: count }, (_, index) => photo(`${prefix}${index}.jpg`))
}

describe('addPhotos', () => {
  it('adds the picked pictures after the ones already chosen, in order', () => {
    const first = photos(2, 'a')
    const picked = photos(2, 'b')

    const result = addPhotos(first, picked)

    expect(result.files.map((file) => file.name)).toEqual(['a0.jpg', 'a1.jpg', 'b0.jpg', 'b1.jpg'])
    expect(result.notice).toBeNull()
  })

  it('accepts exactly nine pictures', () => {
    const result = addPhotos([], photos(MAX_PHOTOS_PER_MESSAGE))

    expect(result.files).toHaveLength(MAX_PHOTOS_PER_MESSAGE)
    expect(result.notice).toBeNull()
  })

  it('keeps the first nine and says so when more are picked at once', () => {
    const result = addPhotos([], photos(MAX_PHOTOS_PER_MESSAGE + 3))

    expect(result.files).toHaveLength(MAX_PHOTOS_PER_MESSAGE)
    expect(result.files[0]?.name).toBe('p0.jpg')
    expect(result.notice).toBe('Можно прикрепить не больше 9 фото.')
  })

  it('fills the free places only, when some pictures are already chosen', () => {
    const chosen = photos(7, 'a')

    const result = addPhotos(chosen, photos(4, 'b'))

    expect(result.files.map((file) => file.name).slice(7)).toEqual(['b0.jpg', 'b1.jpg'])
    expect(result.files).toHaveLength(MAX_PHOTOS_PER_MESSAGE)
    expect(result.notice).toBe('Можно прикрепить не больше 9 фото.')
  })

  it('adds nothing and says so when nine are chosen already', () => {
    const chosen = photos(MAX_PHOTOS_PER_MESSAGE)

    const result = addPhotos(chosen, photos(1, 'b'))

    expect(result.files).toEqual(chosen)
    expect(result.notice).toBe('Можно прикрепить не больше 9 фото.')
  })

  it('leaves the choice as it was when nothing is picked', () => {
    const chosen = photos(2)

    expect(addPhotos(chosen, [])).toEqual({ files: chosen, notice: null })
  })

  it('skips files that are not pictures', () => {
    const result = addPhotos(
      [],
      [photo('notes.pdf', 'application/pdf'), photo('ok.png', 'image/png')],
    )

    expect(result.files.map((file) => file.name)).toEqual(['ok.png'])
    expect(result.notice).toBe('Выберите изображения: JPEG, PNG, WebP или GIF.')
  })

  it('skips a file over the size limit and names it', () => {
    const huge = photo('huge.jpg', 'image/jpeg', PHOTO_MAX_FILE_BYTES + 1)

    const result = addPhotos([], [huge, photo('ok.jpg')])

    expect(result.files.map((file) => file.name)).toEqual(['ok.jpg'])
    expect(result.notice).toBe('Файл «huge.jpg» больше 12 МБ.')
  })

  it('accepts a file exactly at the size limit', () => {
    const edge = photo('edge.jpg', 'image/jpeg', PHOTO_MAX_FILE_BYTES)

    expect(addPhotos([], [edge]).files).toEqual([edge])
  })

  it('does not change the list it was given', () => {
    const chosen = photos(1)

    addPhotos(chosen, photos(2, 'b'))

    expect(chosen).toHaveLength(1)
  })
})

describe('removePhoto', () => {
  it('drops the picture at the index and keeps the order of the rest', () => {
    const chosen = photos(3)

    expect(removePhoto(chosen, 1).map((file) => file.name)).toEqual(['p0.jpg', 'p2.jpg'])
  })

  it('ignores an index that is not in the list', () => {
    const chosen = photos(2)

    expect(removePhoto(chosen, 5)).toEqual(chosen)
    expect(removePhoto(chosen, -1)).toEqual(chosen)
  })
})
