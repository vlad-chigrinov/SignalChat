import { describe, expect, it } from 'vitest'

import { ApiError } from '@/api'
import { HubCallError, NotConnectedError } from '@/realtime/chatHub'

import { errorMessage } from '../errorMessage'

const GENERIC = 'Не удалось связаться с сервером. Попробуйте ещё раз.'

describe('errorMessage', () => {
  it('tells the user that nothing was sent when the hub is not connected', () => {
    expect(errorMessage(new NotConnectedError())).toBe('Нет соединения. Сообщение не отправлено.')
  })

  it('maps hub error codes to user text', () => {
    expect(errorMessage(new HubCallError('not_found'))).toBe('Чат не найден.')
    expect(errorMessage(new HubCallError('rate_limited'))).toBe(
      'Слишком много сообщений подряд. Подождите немного.',
    )
    expect(errorMessage(new HubCallError('unauthenticated'))).toBe('Сессия истекла. Войдите снова.')
  })

  it('uses the generic text for an unknown hub error', () => {
    expect(errorMessage(new HubCallError('unknown'))).toBe(GENERIC)
  })

  it('keeps the REST mapping', () => {
    expect(errorMessage(new ApiError(404, 'x'))).toBe('Чат не найден.')
    expect(errorMessage(new ApiError(500, 'x'))).toBe(GENERIC)
  })

  it('never exposes a raw error message', () => {
    expect(errorMessage(new Error('stack trace with secrets'))).toBe(GENERIC)
  })
})
