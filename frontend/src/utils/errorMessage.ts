import { ApiError } from '@/api'
import { HubCallError, NotConnectedError } from '@/realtime/chatHub'
import type { HubErrorCode } from '@/realtime/hubContract'

import { API_ERROR_TEXT, isApiErrorCode } from './apiErrorText'

const HTTP_NOT_FOUND = 404
const GENERIC_ERROR = 'Не удалось связаться с сервером. Попробуйте ещё раз.'
const NOT_CONNECTED_TEXT = 'Нет соединения. Сообщение не отправлено.'

const HUB_ERROR_TEXT: Record<HubErrorCode, string> = {
  not_found: 'Чат не найден.',
  invalid_message: 'Сообщение пустое или слишком длинное.',
  rate_limited: 'Слишком много сообщений подряд. Подождите немного.',
  too_many_groups: 'Открыто слишком много чатов. Закройте часть и попробуйте снова.',
  unauthenticated: 'Сессия истекла. Войдите снова.',
  unknown: GENERIC_ERROR,
}

function apiErrorText(error: ApiError): string {
  if (error.code !== null && isApiErrorCode(error.code)) return API_ERROR_TEXT[error.code]
  if (error.status === HTTP_NOT_FOUND) return API_ERROR_TEXT.not_found
  return GENERIC_ERROR
}

/** User-facing text for a failed request. Never shows raw server output. */
export function errorMessage(error: unknown): string {
  if (error instanceof NotConnectedError) return NOT_CONNECTED_TEXT
  if (error instanceof HubCallError) return HUB_ERROR_TEXT[error.code]
  if (error instanceof ApiError) return apiErrorText(error)
  return GENERIC_ERROR
}
