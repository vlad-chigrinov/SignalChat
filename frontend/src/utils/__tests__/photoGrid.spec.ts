import { describe, expect, it } from 'vitest'

import {
  GRID_COLUMNS,
  SINGLE_PHOTO_MAX_ASPECT,
  SINGLE_PHOTO_MIN_ASPECT,
  photoGridCells,
} from '../photoGrid'

function spans(count: number): number[] {
  return photoGridCells(count, [{ width: 100, height: 100 }]).map((cell) => cell.span)
}

function sizes(count: number, width = 100, height = 100) {
  return Array.from({ length: count }, () => ({ width, height }))
}

describe('photoGridCells', () => {
  it('has one cell per photo', () => {
    for (let count = 1; count <= 9; count += 1) {
      expect(photoGridCells(count, sizes(count))).toHaveLength(count)
    }
  })

  it('shows one photo large, on the whole row', () => {
    expect(spans(1)).toEqual([GRID_COLUMNS])
  })

  it('puts two photos side by side', () => {
    expect(spans(2)).toEqual([3, 3])
  })

  it('shows three photos as one large and two smaller', () => {
    expect(spans(3)).toEqual([6, 3, 3])
  })

  it('lays four photos out two by two', () => {
    expect(spans(4)).toEqual([3, 3, 3, 3])
  })

  it('lays five to nine photos out three to a row, the last row sharing the whole width', () => {
    expect(spans(5)).toEqual([2, 2, 2, 3, 3])
    expect(spans(6)).toEqual([2, 2, 2, 2, 2, 2])
    expect(spans(7)).toEqual([2, 2, 2, 2, 2, 2, 6])
    expect(spans(8)).toEqual([2, 2, 2, 2, 2, 2, 3, 3])
    expect(spans(9)).toEqual([2, 2, 2, 2, 2, 2, 2, 2, 2])
  })

  it('fills every row completely, whatever the count', () => {
    for (let count = 2; count <= 9; count += 1) {
      const total = spans(count).reduce((sum, span) => sum + span, 0)
      expect(total % GRID_COLUMNS).toBe(0)
    }
  })

  it('gives a single photo the proportions it has, within sensible bounds', () => {
    const landscape = photoGridCells(1, sizes(1, 1600, 1200))[0]
    const panorama = photoGridCells(1, sizes(1, 4000, 500))[0]
    const tall = photoGridCells(1, sizes(1, 500, 4000))[0]

    expect(landscape?.aspectRatio).toBeCloseTo(4 / 3)
    expect(panorama?.aspectRatio).toBe(SINGLE_PHOTO_MAX_ASPECT)
    expect(tall?.aspectRatio).toBe(SINGLE_PHOTO_MIN_ASPECT)
  })

  it('crops the photos of a group to fixed shapes, so the layout does not depend on the pictures', () => {
    const cells = photoGridCells(3, [
      { width: 4000, height: 500 },
      { width: 500, height: 4000 },
      { width: 100, height: 100 },
    ])

    expect(cells.map((cell) => cell.aspectRatio)).toEqual([16 / 9, 1, 1])
  })

  it('gives a wide last cell a wide shape', () => {
    const cells = photoGridCells(7, sizes(7))

    expect(cells.at(-1)?.aspectRatio).toBe(16 / 9)
  })

  it('survives a photo of unknown size', () => {
    expect(photoGridCells(1, [{ width: 0, height: 0 }])[0]?.aspectRatio).toBe(4 / 3)
  })

  it('returns no cells for no photos', () => {
    expect(photoGridCells(0, [])).toEqual([])
  })
})
