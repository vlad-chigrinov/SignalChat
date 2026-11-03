import { describe, expect, it } from 'vitest'

import { ApiError } from '../http'
import { createSeedStore, mockCurrentUser } from '../mock/mockData'
import { historyOf } from '../mock/mockHistories'
import { chooseReaction, reactionsOf, withReactions } from '../mock/mockReactions'

const ME = mockCurrentUser.id
const ANNA = 'u-anna'
const GENERAL = 'room-general'
const OLGA_REQUEST = 'dm-request-olga'
const CHESS = 'room-chess'
/** In the seed: a 👍 from Olga and a 😂 from Igor are already there. */
const SHARED = 'room-general-m93'
/** In the seed: a single ❤️ from Anna. */
const SINGLE = 'room-general-m92'
const PLAIN = 'room-general-m91'

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

describe('mock reactions', () => {
  it('seeds a message with one reaction and one where a second 👍 will make a count', () => {
    const store = createSeedStore()

    expect(reactionsOf(store, SINGLE, ME)).toEqual([{ emoji: '❤️', count: 1, reactedByMe: false }])
    expect(reactionsOf(store, SHARED, ME).map((reaction) => reaction.emoji)).toEqual(['👍', '😂'])
    expect(reactionsOf(store, PLAIN, ME)).toEqual([])
  })

  it('adds a first reaction, marked as mine', () => {
    const store = createSeedStore()

    const reactions = chooseReaction(store, ME, GENERAL, PLAIN, '👍')

    expect(reactions).toEqual([{ emoji: '👍', count: 1, reactedByMe: true }])
  })

  it('counts a second person with the same emoji', () => {
    const store = createSeedStore()

    const reactions = chooseReaction(store, ME, GENERAL, SHARED, '👍')

    expect(reactions).toContainEqual({ emoji: '👍', count: 2, reactedByMe: true })
    expect(reactionsOf(store, SHARED, ANNA)).toContainEqual({
      emoji: '👍',
      count: 2,
      reactedByMe: false,
    })
  })

  it('replaces my reaction when I choose another one: never two at once', () => {
    const store = createSeedStore()
    chooseReaction(store, ME, GENERAL, PLAIN, '👍')

    const reactions = chooseReaction(store, ME, GENERAL, PLAIN, '❤️')

    expect(reactions).toEqual([{ emoji: '❤️', count: 1, reactedByMe: true }])
    expect(store.reactions.get(PLAIN)?.size).toBe(1)
  })

  it('removes my reaction when I choose the same one again', () => {
    const store = createSeedStore()
    chooseReaction(store, ME, GENERAL, PLAIN, '👍')

    expect(chooseReaction(store, ME, GENERAL, PLAIN, '👍')).toEqual([])
  })

  it('refuses an emoji outside the catalog', () => {
    const store = createSeedStore()

    expectCode(() => chooseReaction(store, ME, GENERAL, PLAIN, '💩'), 'invalid_reaction')
    expectCode(() => chooseReaction(store, ME, GENERAL, PLAIN, '<b>'), 'invalid_reaction')
    expect(reactionsOf(store, PLAIN, ME)).toEqual([])
  })

  it('answers not_found for a room the caller is not in, or a message of another conversation', () => {
    const store = createSeedStore()

    expectCode(() => chooseReaction(store, ME, CHESS, 'room-chess-m1', '👍'), 'not_found')
    expectCode(() => chooseReaction(store, ME, 'room-dev', PLAIN, '👍'), 'not_found')
    expectCode(() => chooseReaction(store, ME, GENERAL, 'missing', '👍'), 'not_found')
  })

  it('does not let the recipient of a pending request react, nor its writer', () => {
    const store = createSeedStore()

    expectCode(
      () => chooseReaction(store, ME, OLGA_REQUEST, 'dm-request-olga-m1', '👍'),
      'not_found',
    )
    expectCode(
      () => chooseReaction(store, 'u-olga', OLGA_REQUEST, 'dm-request-olga-m1', '👍'),
      'dm_not_accepted',
    )
  })

  it('shows history items with the reactions of the viewer', () => {
    const store = createSeedStore()
    chooseReaction(store, ME, GENERAL, SHARED, '👍')
    const items = historyOf(store, GENERAL).filter((message) => message.id === SHARED)

    const [shown] = withReactions(store, items, ME)

    expect(shown?.reactions).toContainEqual({ emoji: '👍', count: 2, reactedByMe: true })
  })
})
