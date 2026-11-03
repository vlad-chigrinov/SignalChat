const FULL_TURN_DEGREES = 360
const QUARTER_TURN_DEGREES = 90
const HALF_TURN_DEGREES = 180

/** One quarter turn clockwise; after four turns the photo is upright again. */
export function nextRotation(degrees: number): number {
  return (degrees + QUARTER_TURN_DEGREES) % FULL_TURN_DEGREES
}

/** A quarter or three-quarter turn swaps the photo's width and height on screen. */
export function isSideways(degrees: number): boolean {
  return degrees % HALF_TURN_DEGREES !== 0
}

/** The index `step` places away, or null at the ends of the set: the viewer does not wrap around. */
export function neighbourIndex(index: number, count: number, step: 1 | -1): number | null {
  const target = index + step
  return target >= 0 && target < count ? target : null
}

/** A horizontal move shorter than this is a tap or a wobble of the finger, not a swipe. */
export const SWIPE_MIN_DISTANCE_PX = 50
/** The move must be this many times longer sideways than up or down, so scrolling gestures do not page the photos. */
const SWIPE_HORIZONTAL_DOMINANCE = 1.5

/** Which photo a finger move from press to release asks for: left shows the next one, right the previous one. */
export function swipeStep(deltaX: number, deltaY: number): 1 | -1 | null {
  if (Math.abs(deltaX) < SWIPE_MIN_DISTANCE_PX) return null
  if (Math.abs(deltaX) < Math.abs(deltaY) * SWIPE_HORIZONTAL_DOMINANCE) return null
  return deltaX < 0 ? 1 : -1
}
