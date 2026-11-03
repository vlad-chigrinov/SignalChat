/** Hands the browser a file to save under `fileName`; the page stays where it is. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.hidden = true
  document.body.append(link)
  link.click()
  link.remove()
  // The download has started by the time the task ends; the address is not needed longer.
  setTimeout(() => URL.revokeObjectURL(url), 0)
}
