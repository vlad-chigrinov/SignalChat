import { describe, expect, it } from 'vitest'

import { ApiError } from '@/api'
import { API_ERROR_TEXT } from '@/utils/apiErrorText'
import { HubCallError } from '@/realtime/chatHub'

import { errorMessage } from '../errorMessage'

const GENERIC_TEXT = 'Не удалось связаться с сервером. Попробуйте ещё раз.'
const HTTP_NOT_FOUND = 404
const HTTP_CONFLICT = 409

describe('errorMessage', () => {
  it('maps a known REST code to its Russian text', () => {
    const error = new ApiError(HTTP_CONFLICT, 'conflict', 'room_name_taken')

    expect(errorMessage(error)).toBe(API_ERROR_TEXT.room_name_taken)
  })

  it('falls back to the not_found text for a 404 without a known code', () => {
    const error = new ApiError(HTTP_NOT_FOUND, 'missing', null)

    expect(errorMessage(error)).toBe(API_ERROR_TEXT.not_found)
  })

  it('never shows the raw code or message for an unknown code', () => {
    const error = new ApiError(HTTP_CONFLICT, 'raw server text', 'toString')

    expect(errorMessage(error)).toBe(GENERIC_TEXT)
  })

  it('maps a hub error through the hub text table', () => {
    expect(errorMessage(new HubCallError('rate_limited'))).toMatch(/Слишком много/)
  })

  it('uses the generic text for anything else', () => {
    expect(errorMessage(new Error('boom'))).toBe(GENERIC_TEXT)
  })
})
