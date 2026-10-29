import type { CurrentUser, DirectConversation, Message, Room, UserSummary } from '../types'

const MINUTE_MS = 60_000
const MESSAGE_SPACING_MINUTES = 7
const GENERAL_ROOM_MESSAGE_COUNT = 95
const DEV_ROOM_MESSAGE_COUNT = 12

export const mockCurrentUser: CurrentUser = {
  id: 'u-me',
  displayName: 'Вы (демо)',
  avatarUrl: null,
  email: 'demo@example.com',
}

const anna: UserSummary = { id: 'u-anna', displayName: 'Анна Смирнова', avatarUrl: null }
const igor: UserSummary = { id: 'u-igor', displayName: 'Игорь Петров', avatarUrl: null }
const olga: UserSummary = { id: 'u-olga', displayName: 'Ольга Ким', avatarUrl: null }

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

export function createMockHistories(): Map<string, Message[]> {
  const generalAuthors = [anna, igor, mockCurrentUser, olga]
  return new Map([
    ['room-general', buildHistory('room-general', generalAuthors, GENERAL_ROOM_MESSAGE_COUNT)],
    ['room-dev', buildHistory('room-dev', [igor, mockCurrentUser], DEV_ROOM_MESSAGE_COUNT)],
    ['room-quiet', []],
    ['dm-anna', buildHistory('dm-anna', [anna, mockCurrentUser], 8)],
    ['dm-igor', buildHistory('dm-igor', [mockCurrentUser, igor], 3)],
  ])
}

export const mockRooms: Omit<Room, 'lastMessage'>[] = [
  { kind: 'room', id: 'room-general', name: 'Общий', description: 'Обо всём', memberCount: 42 },
  { kind: 'room', id: 'room-dev', name: 'Разработка', description: 'Код и релизы', memberCount: 9 },
  { kind: 'room', id: 'room-quiet', name: 'Тихая', description: 'Пока пусто', memberCount: 1 },
]

export const mockDirects: Omit<DirectConversation, 'lastMessage'>[] = [
  { kind: 'direct', id: 'dm-anna', peer: anna },
  { kind: 'direct', id: 'dm-igor', peer: igor },
]
