import { describe, expect, it } from 'vitest'

import { previewText } from '../messagePreview'

describe('previewText', () => {
  it('is the text of a text message', () => {
    expect(previewText({ text: 'привет', photoCount: 0 })).toBe('привет')
  })

  it('names a lone photo without a caption', () => {
    expect(previewText({ text: '', photoCount: 1 })).toBe('Фото')
  })

  it('counts several photos', () => {
    expect(previewText({ text: '', photoCount: 4 })).toBe('Фото ×4')
  })

  it('puts the caption after the photos', () => {
    expect(previewText({ text: 'закат', photoCount: 2 })).toBe('Фото ×2: закат')
  })
})
