import { FETCH_CREDENTIALS, attachmentDownloadSrc } from './apiConfig'

/**
 * The bytes of a photo, read with the session cookie. Saving goes through these bytes instead of a link, so the page does
 * not navigate away and no tab opens, whatever the browser does with a cross-origin `download` attribute.
 */
export async function fetchAttachmentFile(attachmentUrl: string): Promise<Blob> {
  const src = attachmentDownloadSrc(attachmentUrl)
  if (src === null) throw new Error('The photo has no usable address.')
  const response = await fetch(src, { credentials: FETCH_CREDENTIALS })
  if (!response.ok) throw new Error(`Photo download failed: ${response.status}`)
  return response.blob()
}
