import { MAX_PHOTOS_PER_MESSAGE, PHOTO_ALLOWED_TYPES, PHOTO_MAX_FILE_BYTES } from '@/api/limits'

const BYTES_PER_MEBIBYTE = 1024 * 1024

export interface PhotoSelection {
  files: File[]
  /** What the user is told about the files that were left out; null when all were taken. */
  notice: string | null
}

function isPicture(file: File): boolean {
  return PHOTO_ALLOWED_TYPES.includes(file.type)
}

/**
 * Adds the pictures the user picked to the ones already chosen: only pictures the server decodes, none over the size
 * limit, at most nine in all. What is left out is reported in `notice`; the input list is not changed.
 */
export function addPhotos(current: readonly File[], picked: readonly File[]): PhotoSelection {
  let notice: string | null = null
  const accepted: File[] = []
  for (const file of picked) {
    if (!isPicture(file)) {
      notice ??= 'Выберите изображения: JPEG, PNG, WebP или GIF.'
    } else if (file.size > PHOTO_MAX_FILE_BYTES) {
      notice ??= `Файл «${file.name}» больше ${PHOTO_MAX_FILE_BYTES / BYTES_PER_MEBIBYTE} МБ.`
    } else {
      accepted.push(file)
    }
  }

  const free = Math.max(0, MAX_PHOTOS_PER_MESSAGE - current.length)
  if (accepted.length > free) {
    notice ??= `Можно прикрепить не больше ${MAX_PHOTOS_PER_MESSAGE} фото.`
  }
  return { files: [...current, ...accepted.slice(0, free)], notice }
}

/** The list without the picture at `index`; an index outside the list leaves it as it was. */
export function removePhoto(files: readonly File[], index: number): File[] {
  return files.filter((_, position) => position !== index)
}
