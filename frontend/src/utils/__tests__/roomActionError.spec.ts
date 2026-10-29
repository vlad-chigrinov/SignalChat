import { describe, expect, it } from 'vitest'

import { ApiError } from '@/api'
import { API_ERROR_TEXT } from '@/utils/apiErrorText'

import { roomActionFailure } from '../roomActionError'

const HTTP_NOT_FOUND = 404
const HTTP_CONFLICT = 409

describe('roomActionFailure', () => {
  it('reports a 404 as an unavailable room and asks for a reload', () => {
    const failure = roomActionFailure(new ApiError(HTTP_NOT_FOUND, 'missing', 'not_found'))

    expect(failure.isUnavailable).toBe(true)
    expect(failure.message).toMatch(/Комната недоступна/)
  })

  it('shows the owner rule in words and does not ask for a reload', () => {
    const failure = roomActionFailure(new ApiError(HTTP_CONFLICT, 'owner', 'owner_cannot_leave'))

    expect(failure).toEqual({
      message: API_ERROR_TEXT.owner_cannot_leave,
      isUnavailable: false,
    })
  })
})
