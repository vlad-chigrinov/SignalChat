import { ApiError } from '@/api'
import { HubCallError, NotConnectedError } from '@/realtime/chatHub'
import type { HubErrorCode } from '@/realtime/hubContract'

const HTTP_FORBIDDEN = 403
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

/** User-facing text for a failed request. Never shows raw server output. */
export function errorMessage(error: unknown): string {
  if (error instanceof NotConnectedError) return NOT_CONNECTED_TEXT
  if (error instanceof HubCallError) return HUB_ERROR_TEXT[error.code]
  if (error instanceof ApiError) {
    if (error.status === HTTP_FORBIDDEN) return 'Нет доступа к этому чату.'
    if (error.status === HTTP_NOT_FOUND) return 'Чат не найден.'
  }
  return GENERIC_ERROR
}
