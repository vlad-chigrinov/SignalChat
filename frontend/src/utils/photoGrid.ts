/** The feed lays photos out on a grid of this many columns; a cell spans some of them. */
export const GRID_COLUMNS = 6
/** A lone photo keeps its own proportions, but a panorama or a tall strip is cropped to these bounds. */
export const SINGLE_PHOTO_MIN_ASPECT = 3 / 4
export const SINGLE_PHOTO_MAX_ASPECT = 16 / 9

const UNKNOWN_SIZE_ASPECT = 4 / 3
const WIDE_CELL_ASPECT = 16 / 9
const SQUARE_ASPECT = 1
const COLUMNS_PER_THIRD = GRID_COLUMNS / 3
const COLUMNS_PER_HALF = GRID_COLUMNS / 2
const PHOTOS_PER_ROW = 3

export interface PhotoSize {
  width: number
  height: number
}

export interface PhotoGridCell {
  /** Columns of the grid the cell takes (of {@link GRID_COLUMNS}). */
  span: number
  /** Width over height of the cell; the picture is cropped to it from the centre. */
  aspectRatio: number
}

function spansFor(count: number): number[] {
  if (count === 2) return [COLUMNS_PER_HALF, COLUMNS_PER_HALF]
  if (count === 3) return [GRID_COLUMNS, COLUMNS_PER_HALF, COLUMNS_PER_HALF]
  if (count === 4) return Array<number>(4).fill(COLUMNS_PER_HALF)

  // Three to a row; the last, incomplete row shares the whole width, so no row has a gap.
  const spans = Array<number>(count).fill(COLUMNS_PER_THIRD)
  const lastRow = count % PHOTOS_PER_ROW
  if (lastRow === 1) spans[count - 1] = GRID_COLUMNS
  if (lastRow === 2) spans.fill(COLUMNS_PER_HALF, count - 2)
  return spans
}

function singleAspect({ width, height }: PhotoSize): number {
  if (width <= 0 || height <= 0) return UNKNOWN_SIZE_ASPECT
  return Math.min(Math.max(width / height, SINGLE_PHOTO_MIN_ASPECT), SINGLE_PHOTO_MAX_ASPECT)
}

/**
 * The cells of the photo grid of a message, in photo order. The shapes depend on the count and, for a lone photo, on its
 * size, which the server sends with the message, so the space is reserved before any picture has loaded.
 */
export function photoGridCells(count: number, sizes: readonly PhotoSize[]): PhotoGridCell[] {
  if (count <= 0) return []
  if (count === 1) {
    return [{ span: GRID_COLUMNS, aspectRatio: singleAspect(sizes[0] ?? { width: 0, height: 0 }) }]
  }

  return spansFor(count).map((span) => ({
    span,
    aspectRatio: span === GRID_COLUMNS ? WIDE_CELL_ASPECT : SQUARE_ASPECT,
  }))
}
