export type PickerPlacement = 'above' | 'below'

/** Height of the reaction panel plus the gap to the message, with a little spare room. */
export const PICKER_HEIGHT_PX = 56

/**
 * The panel opens above the message. Near the top of the message list there is no room for it
 * (the list clips what sticks out), so it opens below instead.
 */
export function pickerPlacement(spaceAbovePx: number): PickerPlacement {
  return spaceAbovePx >= PICKER_HEIGHT_PX ? 'above' : 'below'
}
