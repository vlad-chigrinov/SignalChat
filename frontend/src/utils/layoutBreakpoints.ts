/** From this viewport width the chat list stays on the left and the chat fills the rest of the screen. */
export const SIDEBAR_MIN_WIDTH_PX = 720
/** From this viewport width the open conversation also shows its participants on the right. */
export const PARTICIPANTS_MIN_WIDTH_PX = 1080

export function minWidthQuery(widthPx: number): string {
  return `(min-width: ${widthPx}px)`
}
