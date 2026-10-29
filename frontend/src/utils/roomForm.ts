import { API_ERROR_TEXT } from '@/utils/apiErrorText'
import { ApiError } from '@/api'
import { errorMessage } from '@/utils/errorMessage'

const EMPTY_ROOM_NAME_TEXT = 'Введите название комнаты.'

/** Client-side check of the room name: the server accepts 3..80 characters after trim. */
export function roomNameError(name: string): string | null {
  return name.trim() === '' ? EMPTY_ROOM_NAME_TEXT : null
}

export interface CreateRoomFailure {
  /** Message for the name field, when the name itself is the problem. */
  field: string | null
  /** Message for the whole dialog, for any other failure. */
  form: string | null
}

/** Where a failed create-room request is shown: `room_name_taken` at the name, the rest at the form. */
export function createRoomFailure(error: unknown): CreateRoomFailure {
  if (error instanceof ApiError && error.code === 'room_name_taken') {
    return { field: API_ERROR_TEXT.room_name_taken, form: null }
  }
  return { field: null, form: errorMessage(error) }
}
