/** A type no browser shows by itself, so Firefox saves the file instead of opening the picture in a tab. */
const DOWNLOAD_MIME_TYPE = 'application/octet-stream'
/** Firefox reads the address after the click returns; it is revoked once the download has certainly begun. */
const REVOKE_DELAY_MS = 60_000

/** Hands the browser a file to save under `fileName`; the page stays where it is. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(new Blob([blob], { type: DOWNLOAD_MIME_TYPE }))
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.rel = 'noopener'
  link.hidden = true
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), REVOKE_DELAY_MS)
}
