import { ApiError } from '@/api'

const HTTP_FORBIDDEN = 403
const HTTP_NOT_FOUND = 404

/** User-facing text for a failed request. Never shows raw server output. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === HTTP_FORBIDDEN) return 'Нет доступа к этому чату.'
    if (error.status === HTTP_NOT_FOUND) return 'Чат не найден.'
  }
  return 'Не удалось связаться с сервером. Попробуйте ещё раз.'
}
