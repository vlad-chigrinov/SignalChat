import type { CurrentUser, Message, UserSummary } from '../types'

import type { MockStore } from './mockHistories'

const MINUTE_MS = 60_000
const MESSAGE_SPACING_MINUTES = 7
const GENERAL_ROOM_MESSAGE_COUNT = 95
const DEV_ROOM_MESSAGE_COUNT = 12
const DIRECT_ANNA_MESSAGE_COUNT = 8
const DIRECT_IGOR_MESSAGE_COUNT = 3

export const mockCurrentUser: CurrentUser = {
  id: 'u-me',
  displayName: 'Вы (демо)',
  avatarUrl: null,
  email: 'demo@example.com',
}

const anna: UserSummary = { id: 'u-anna', displayName: 'Анна Смирнова', avatarUrl: null }
const igor: UserSummary = { id: 'u-igor', displayName: 'Игорь Петров', avatarUrl: null }
const olga: UserSummary = { id: 'u-olga', displayName: 'Ольга Ким', avatarUrl: null }

/** Users other than the signed-in one, as the search and direct-conversation rules see them. */
export const mockDirectory: UserSummary[] = [anna, igor, olga]

export const mockOnlineUserIds = [anna.id, olga.id]

const SAMPLE_LINES = [
  'Привет всем!',
  'Кто-нибудь смотрел новый релиз?',
  'Да, выглядит неплохо.',
  '<img src=x onerror=alert(1)> — должно показаться как текст',
  'Многострочное\nсообщение\nс переносами',
  'Встречаемся в 15:00?',
  'Ок 👍',
]

function minutesAgo(minutes: number): string {
  return new Date(Date.now() - minutes * MINUTE_MS).toISOString()
}

function buildHistory(conversationId: string, authors: UserSummary[], count: number): Message[] {
  return Array.from({ length: count }, (_, index) => ({
    id: `${conversationId}-m${index + 1}`,
    conversationId,
    author: authors[index % authors.length] ?? mockCurrentUser,
    text: `${SAMPLE_LINES[index % SAMPLE_LINES.length]} (#${index + 1})`,
    sentAt: minutesAgo((count - index) * MESSAGE_SPACING_MINUTES),
  }))
}

/** The demo data a fresh page starts with. Pure: every call returns a new, independent store. */
export function createSeedStore(): MockStore {
  const me = mockCurrentUser
  const histories = new Map<string, Message[]>([
    [
      'room-general',
      buildHistory('room-general', [anna, igor, me, olga], GENERAL_ROOM_MESSAGE_COUNT),
    ],
    ['room-dev', buildHistory('room-dev', [igor, me], DEV_ROOM_MESSAGE_COUNT)],
    ['room-quiet', []],
    ['room-chess', []],
    ['dm-anna', buildHistory('dm-anna', [anna, me], DIRECT_ANNA_MESSAGE_COUNT)],
    ['dm-igor', buildHistory('dm-igor', [me, igor], DIRECT_IGOR_MESSAGE_COUNT)],
  ])
  return {
    currentUser: me,
    directory: mockDirectory.map((user) => ({ ...user })),
    rooms: new Map([
      [
        'room-general',
        {
          id: 'room-general',
          name: 'Общий',
          description: 'Обо всём',
          ownerId: anna.id,
          memberIds: new Set([anna.id, igor.id, olga.id, me.id]),
        },
      ],
      [
        'room-dev',
        {
          id: 'room-dev',
          name: 'Разработка',
          description: 'Код и релизы',
          ownerId: igor.id,
          memberIds: new Set([igor.id, me.id]),
        },
      ],
      [
        'room-quiet',
        {
          id: 'room-quiet',
          name: 'Тихая',
          description: 'Пока пусто',
          ownerId: me.id,
          memberIds: new Set([me.id]),
        },
      ],
      // Not a member: shows up only in discovery.
      [
        'room-chess',
        {
          id: 'room-chess',
          name: 'Шахматы',
          description: 'Партии и разборы',
          ownerId: anna.id,
          memberIds: new Set([anna.id, igor.id]),
        },
      ],
    ]),
    directs: new Map([
      ['dm-anna', { id: 'dm-anna', memberIds: [me.id, anna.id] }],
      ['dm-igor', { id: 'dm-igor', memberIds: [me.id, igor.id] }],
    ]),
    histories,
    nextId: 1,
  }
}
