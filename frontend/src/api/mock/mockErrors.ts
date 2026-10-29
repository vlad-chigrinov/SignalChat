import { ApiError } from '../http'
import type { ApiErrorCode } from '../types'

const HTTP_BAD_REQUEST = 400
const HTTP_NOT_FOUND = 404
const HTTP_CONFLICT = 409

/** Same status the real server answers for each stable code (see CONTRACT.md). */
const STATUS_BY_CODE: Record<ApiErrorCode, number> = {
  not_found: HTTP_NOT_FOUND,
  invalid_cursor: HTTP_BAD_REQUEST,
  invalid_limit: HTTP_BAD_REQUEST,
  query_too_short: HTTP_BAD_REQUEST,
  invalid_room_name: HTTP_BAD_REQUEST,
  invalid_room_description: HTTP_BAD_REQUEST,
  self_conversation: HTTP_BAD_REQUEST,
  invalid_message: HTTP_BAD_REQUEST,
  room_name_taken: HTTP_CONFLICT,
  room_limit_reached: HTTP_CONFLICT,
  owner_cannot_leave: HTTP_CONFLICT,
  dm_request_pending: HTTP_CONFLICT,
  dm_incoming_request_exists: HTTP_CONFLICT,
  dm_request_limit_reached: HTTP_CONFLICT,
}

/** The same ApiError the HTTP client raises for this ProblemDetails answer. */
export function mockError(code: ApiErrorCode): ApiError {
  return new ApiError(STATUS_BY_CODE[code], `mock ${code}`, code)
}
