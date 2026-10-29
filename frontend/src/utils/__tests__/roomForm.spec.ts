import { describe, expect, it } from 'vitest'

import { ApiError } from '@/api'
import { API_ERROR_TEXT } from '@/utils/apiErrorText'

import { createRoomFailure, roomNameError } from '../roomForm'

const HTTP_CONFLICT = 409
const HTTP_BAD_REQUEST = 400

describe('roomNameError', () => {
  it('rejects a name that is empty after trim', () => {
    expect(roomNameError('   ')).not.toBeNull()
    expect(roomNameError('')).not.toBeNull()
  })

  it('accepts any name with visible characters', () => {
    expect(roomNameError('  Общий  ')).toBeNull()
  })
})

describe('createRoomFailure', () => {
  it('shows room_name_taken at the name field', () => {
    const error = new ApiError(HTTP_CONFLICT, 'taken', 'room_name_taken')

    expect(createRoomFailure(error)).toEqual({
      field: API_ERROR_TEXT.room_name_taken,
      form: null,
    })
  })

  it('shows room_limit_reached for the whole dialog', () => {
    const error = new ApiError(HTTP_CONFLICT, 'limit', 'room_limit_reached')

    expect(createRoomFailure(error)).toEqual({
      field: null,
      form: API_ERROR_TEXT.room_limit_reached,
    })
  })

  it('shows any other failure for the whole dialog', () => {
    const error = new ApiError(HTTP_BAD_REQUEST, 'bad', 'invalid_room_name')

    const failure = createRoomFailure(error)

    expect(failure.field).toBeNull()
    expect(failure.form).toBe(API_ERROR_TEXT.invalid_room_name)
  })
})
