/**
 * What the field holds after a send failed. The text was cleared when the send started, so the
 * user may have typed something new since: the failed text goes first (it is older) and the new
 * text follows on its own line. Nothing is overwritten, so no typing is lost either way.
 */
export function draftAfterFailedSend(failedText: string, currentDraft: string): string {
  if (currentDraft.trim().length === 0) return failedText
  return `${failedText.trimEnd()}\n${currentDraft}`
}
