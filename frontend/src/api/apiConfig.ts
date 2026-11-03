import {
  credentialsFor,
  joinApiUrl,
  normalizeApiOrigin,
  resolveAttachmentSrc,
  resolveAvatarSrc,
} from './apiOrigin'

/** Set at build time (`VITE_API_ORIGIN`); empty in development, where the Vite proxy makes the API same-origin. */
export const API_ORIGIN = normalizeApiOrigin(import.meta.env.VITE_API_ORIGIN)

export const FETCH_CREDENTIALS = credentialsFor(API_ORIGIN)

/** Absolute URL of an API path when the API has its own origin, the path itself otherwise. */
export function apiUrl(path: string): string {
  return joinApiUrl(API_ORIGIN, path)
}

/** The address to put in an `<img src>` for a user's `avatarUrl`. */
export function avatarSrc(avatarUrl: string | null): string | null {
  return resolveAvatarSrc(API_ORIGIN, avatarUrl)
}

/** The address to put in an `<img src>` for a photo's `url` or `thumbnailUrl`; null when the API gave an unusable one. */
export function attachmentSrc(attachmentUrl: string): string | null {
  return resolveAttachmentSrc(API_ORIGIN, attachmentUrl)
}

/** The same photo as a download: the server then names the file and sends it as an attachment. */
export function attachmentDownloadSrc(attachmentUrl: string): string | null {
  const src = attachmentSrc(attachmentUrl)
  return src === null ? null : `${src}${src.includes('?') ? '&' : '?'}download=true`
}
