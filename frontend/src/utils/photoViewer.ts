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
