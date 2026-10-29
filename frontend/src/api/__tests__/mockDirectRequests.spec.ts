import { describe, expect, it } from 'vitest'

import { ApiError } from '../http'
import { createSeedStore, mockCurrentUser } from '../mock/mockData'
import { requireMember } from '../mock/mockHistories'
import {
  OUTGOING_REQUEST_LIMIT,
  acceptRequest,
  declineRequest,
  listIncomingRequests,
  listMyDirects,
  sendDirectRequest,
} from '../mock/mockDirects'
import type { MockStore } from '../mock/mockHistories'

const ME = mockCurrentUser.id
const PETR = 'u-petr'
const OLGA = 'u-olga'
const OLGA_REQUEST = 'dm-request-olga'
const REQUEST_TEXT = 'Привет! Есть минутка?'
const MAX_MESSAGE_LENGTH = 2000

/** Asserts that the rule throws an ApiError with the given stable code. */
function expectCode(action: () => unknown, code: string): void {
  let caught: unknown = null
  try {
    action()
  } catch (error) {
    caught = error
  }
  expect(caught).toBeInstanceOf(ApiError)
  expect((caught as ApiError).code).toBe(code)
}

describe('mock direct requests: sending', () => {
  it('turns the first message into a pending request, visible to its writer', () => {
    const store = createSeedStore()

    const created = sendDirectRequest(store, ME, PETR, REQUEST_TEXT)

    expect(created.status).toBe('pending')
    expect(created.peer.id).toBe(PETR)
    expect(listMyDirects(store, ME)).toContainEqual(expect.objectContaining({ id: created.id }))
    expect(requireMember(store, created.id, ME).at(-1)?.text).toBe(REQUEST_TEXT)
  })

  it('refuses a second request while the first one waits, with the same answer as after a decline', () => {
    const store = createSeedStore()
    sendDirectRequest(store, ME, PETR, REQUEST_TEXT)

    expectCode(() => sendDirectRequest(store, ME, PETR, 'ещё раз'), 'dm_request_pending')
  })

  it('refuses a request to a user whose request to the caller waits for an answer', () => {
    const store = createSeedStore()

    expectCode(() => sendDirectRequest(store, ME, OLGA, REQUEST_TEXT), 'dm_incoming_request_exists')
  })

  it('answers an accepted chat as it is and ignores the text', () => {
    const store = createSeedStore()

    const existing = sendDirectRequest(store, ME, 'u-anna', REQUEST_TEXT)

    expect(existing.id).toBe('dm-anna')
    expect(existing.status).toBe('accepted')
    expect(requireMember(store, 'dm-anna', ME).at(-1)?.text).not.toBe(REQUEST_TEXT)
  })

  it('rejects an empty, blank or too long first message as invalid_message', () => {
    const store = createSeedStore()

    expectCode(() => sendDirectRequest(store, ME, PETR, ''), 'invalid_message')
    expectCode(() => sendDirectRequest(store, ME, PETR, '   '), 'invalid_message')
    expectCode(
      () => sendDirectRequest(store, ME, PETR, 'x'.repeat(MAX_MESSAGE_LENGTH + 1)),
      'invalid_message',
    )
  })

  it('rejects a request to oneself and to an unknown user', () => {
    const store = createSeedStore()

    expectCode(() => sendDirectRequest(store, ME, ME, REQUEST_TEXT), 'self_conversation')
    expectCode(() => sendDirectRequest(store, ME, 'u-nobody', REQUEST_TEXT), 'not_found')
  })

  it('caps the requests waiting for an answer per user', () => {
    const store = createSeedStore()
    const extraUsers = Array.from({ length: OUTGOING_REQUEST_LIMIT + 1 }, (_, index) => ({
      id: `u-extra-${index}`,
      displayName: `Пользователь ${index}`,
      avatarUrl: null,
    }))
    store.directory.push(...extraUsers)

    extraUsers
      .slice(0, OUTGOING_REQUEST_LIMIT)
      .forEach((user) => sendDirectRequest(store, ME, user.id, REQUEST_TEXT))

    expectCode(
      () => sendDirectRequest(store, ME, `u-extra-${OUTGOING_REQUEST_LIMIT}`, REQUEST_TEXT),
      'dm_request_limit_reached',
    )
  })
})

describe('mock direct requests: answering', () => {
  it('lists the requests the caller has to answer, with the newest text', () => {
    const store = createSeedStore()

    const incoming = listIncomingRequests(store, ME)

    expect(incoming).toEqual([
      expect.objectContaining({
        id: OLGA_REQUEST,
        from: expect.objectContaining({ id: OLGA }),
        text: 'Здравствуйте! Можно обсудить релиз?',
      }),
    ])
  })

  it('makes an accepted request a chat for both sides', () => {
    const store = createSeedStore()

    acceptRequest(store, ME, OLGA_REQUEST)

    expect(listIncomingRequests(store, ME)).toEqual([])
    expect(listMyDirects(store, ME)).toContainEqual(
      expect.objectContaining({ id: OLGA_REQUEST, status: 'accepted' }),
    )
    expect(requireMember(store, OLGA_REQUEST, ME).length).toBeGreaterThan(0)
  })

  it('declines silently: the writer still sees the request as pending', () => {
    const store = createSeedStore()
    const request = sendDirectRequest(store, PETR, ME, REQUEST_TEXT)

    declineRequest(store, ME, request.id)

    expect(listMyDirects(store, ME).map((direct) => direct.id)).not.toContain(request.id)
    expect(listMyDirects(store, PETR)).toContainEqual(
      expect.objectContaining({ id: request.id, status: 'pending' }),
    )
  })

  it('lets the decliner write first again, which reopens the request with them as writer', () => {
    const store = createSeedStore()
    const request = sendDirectRequest(store, PETR, ME, REQUEST_TEXT)
    declineRequest(store, ME, request.id)

    const reopened = sendDirectRequest(store, ME, PETR, 'Всё-таки пишу сам')

    expect(reopened.id).toBe(request.id)
    expect(listIncomingRequests(store, PETR)).toEqual([
      expect.objectContaining({ id: request.id, text: 'Всё-таки пишу сам' }),
    ])
  })

  it('refuses the initiator a second request after a decline, the same as while pending', () => {
    const store = createSeedStore()
    const request = sendDirectRequest(store, PETR, ME, REQUEST_TEXT)
    declineRequest(store, ME, request.id)

    expectCode(() => sendDirectRequest(store, PETR, ME, 'ещё раз'), 'dm_request_pending')
  })

  it('answers not_found to anyone who is not the recipient, or for an answered request', () => {
    const store = createSeedStore()

    expectCode(() => acceptRequest(store, PETR, OLGA_REQUEST), 'not_found')
    acceptRequest(store, ME, OLGA_REQUEST)
    expectCode(() => declineRequest(store, ME, OLGA_REQUEST), 'not_found')
  })
})

describe('mock direct requests: reading', () => {
  it('shows a pending request to its writer only, never to the recipient', () => {
    const store: MockStore = createSeedStore()
    const request = sendDirectRequest(store, ME, PETR, REQUEST_TEXT)

    expect(requireMember(store, request.id, ME).length).toBeGreaterThan(0)
    expectCode(() => requireMember(store, request.id, PETR), 'not_found')
    expectCode(() => requireMember(store, OLGA_REQUEST, ME), 'not_found')
  })
})
