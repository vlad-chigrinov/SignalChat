import { beforeEach, describe, expect, it } from 'vitest'

import type { UserSummary } from '@/api'
import { acceptRequest, sendDirectRequest } from '@/api/mock/mockDirects'
import { mockCurrentUser } from '@/api/mock/mockData'
import { resetSharedMockStore, sharedMockStore } from '@/api/mock/mockHistories'

import { HubCallError } from '../chatHub'
import { FakeChatHub } from '../fakeChatHub'

const ME: UserSummary = {
  id: mockCurrentUser.id,
  displayName: mockCurrentUser.displayName,
  avatarUrl: mockCurrentUser.avatarUrl,
}
const PETR = 'u-petr'
/** Olga wrote to the signed-in user: the request waits for the signed-in user's answer. */
const INCOMING_REQUEST = 'dm-request-olga'

/** The hub code the call failed with, or undefined when it succeeded. */
async function failureCodeOf(action: Promise<unknown>): Promise<string | undefined> {
  try {
    await action
    return undefined
  } catch (error) {
    return error instanceof HubCallError ? error.code : undefined
  }
}

describe('FakeChatHub: direct requests', () => {
  beforeEach(() => {
    resetSharedMockStore()
  })

  it('refuses the writer of a waiting request with dm_not_accepted', async () => {
    const hub = new FakeChatHub({ currentUser: () => ME })
    await hub.start()
    const request = sendDirectRequest(sharedMockStore(), ME.id, PETR, 'Привет')

    expect(await failureCodeOf(hub.sendMessage(request.id, 'ещё одно'))).toBe('dm_not_accepted')
  })

  it('answers not_found to the recipient of a waiting request, for sending and joining', async () => {
    const hub = new FakeChatHub({ currentUser: () => ME })
    await hub.start()

    expect(await failureCodeOf(hub.sendMessage(INCOMING_REQUEST, 'ответ'))).toBe('not_found')
    expect(await failureCodeOf(hub.joinRoom(INCOMING_REQUEST))).toBe('not_found')
  })

  it('refuses joining a waiting request with not_found, even for its writer', async () => {
    const hub = new FakeChatHub({ currentUser: () => ME })
    await hub.start()
    const request = sendDirectRequest(sharedMockStore(), ME.id, PETR, 'Привет')

    expect(await failureCodeOf(hub.joinRoom(request.id))).toBe('not_found')
  })

  it('lets both sides write once the recipient has accepted', async () => {
    const hub = new FakeChatHub({ currentUser: () => ME })
    await hub.start()
    acceptRequest(sharedMockStore(), ME.id, INCOMING_REQUEST)

    const sent = await hub.sendMessage(INCOMING_REQUEST, 'Да, давай')

    expect(sent.text).toBe('Да, давай')
    expect(await failureCodeOf(hub.joinRoom(INCOMING_REQUEST))).toBeUndefined()
  })
})
