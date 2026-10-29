import type { ApiErrorCode } from '@/api'

/** Russian text for each stable REST error code. The server's own message is never shown. */
export const API_ERROR_TEXT: Record<ApiErrorCode, string> = {
  not_found: 'Чат не найден.',
  invalid_cursor: 'Не удалось загрузить историю. Обновите страницу.',
  invalid_limit: 'Не удалось загрузить историю. Обновите страницу.',
  query_too_short: 'Введите минимум 2 символа.',
  invalid_room_name: 'Название комнаты: от 3 до 80 символов.',
  invalid_room_description: 'Описание комнаты: не больше 300 символов.',
  room_name_taken: 'Комната с таким названием уже есть.',
  room_limit_reached: 'Можно создать не больше 20 комнат.',
  owner_cannot_leave: 'Владелец не может выйти, пока в комнате есть другие участники.',
  self_conversation: 'Нельзя начать чат с самим собой.',
  invalid_message: 'Сообщение пустое или слишком длинное.',
  dm_request_pending: 'Запрос уже отправлен — ждём ответа.',
  dm_incoming_request_exists: 'Этот пользователь уже написал вам — примите запрос.',
  dm_request_limit_reached: 'Можно отправить не больше 20 запросов, которые ждут ответа.',
}

export function isApiErrorCode(code: string): code is ApiErrorCode {
  return Object.hasOwn(API_ERROR_TEXT, code)
}
