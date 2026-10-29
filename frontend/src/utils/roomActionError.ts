import { ApiError } from '@/api'
import { errorMessage } from '@/utils/errorMessage'

const HTTP_NOT_FOUND = 404
const ROOM_UNAVAILABLE_TEXT = 'Комната недоступна. Список обновлён.'

export interface RoomActionFailure {
  message: string
  /** The room is gone or the user is no longer a member: the caller should reload its list. */
  isUnavailable: boolean
}

/** Text for a failed join or leave. A 404 means the room is unavailable (C3: same for non-members). */
export function roomActionFailure(error: unknown): RoomActionFailure {
  if (error instanceof ApiError && error.status === HTTP_NOT_FOUND) {
    return { message: ROOM_UNAVAILABLE_TEXT, isUnavailable: true }
  }
  return { message: errorMessage(error), isUnavailable: false }
}
