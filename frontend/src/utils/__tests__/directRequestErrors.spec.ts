import { describe, expect, it } from 'vitest'

import { ApiError } from '@/api'
import { HubCallError } from '@/realtime/chatHub'
import { parseHubErrorCode } from '@/realtime/hubErrors'
import { API_ERROR_TEXT } from '@/utils/apiErrorText'
import { errorMessage } from '@/utils/errorMessage'

const HTTP_CONFLICT = 409

describe('direct request errors', () => {
  it('shows the REST codes of a request in Russian', () => {
    const error = new ApiError(HTTP_CONFLICT, 'conflict', 'dm_request_pending')

    expect(errorMessage(error)).toBe(API_ERROR_TEXT.dm_request_pending)
    expect(API_ERROR_TEXT.dm_request_pending).toContain('ждём ответа')
    expect(API_ERROR_TEXT.dm_incoming_request_exists).toContain('примите запрос')
    expect(API_ERROR_TEXT.dm_request_limit_reached).toContain('20')
  })

  it('shows the hub code dm_not_accepted in Russian', () => {
    expect(errorMessage(new HubCallError('dm_not_accepted'))).toBe(
      'Собеседник ещё не принял запрос.',
    )
  })

  it('reads dm_not_accepted from the text of a HubException', () => {
    const text = 'Microsoft.AspNetCore.SignalR.HubException: dm_not_accepted'

    expect(parseHubErrorCode(text)).toBe('dm_not_accepted')
  })
})
