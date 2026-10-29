import { describe, expect, it } from 'vitest'

import { ApiError } from '@/api'
import { HubCallError } from '@/realtime/chatHub'

import { errorMessage } from '../errorMessage'

const GENERIC = 'Не удалось связаться с сервером. Попробуйте ещё раз.'

describe('errorMessage', () => {
  it('maps hub error codes to user text', () => {
    expect(errorMessage(new HubCallError('forbidden'))).toBe('Нет доступа к этому чату.')
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
