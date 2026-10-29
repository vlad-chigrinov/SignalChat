import { DISCOVER_MAX_LIMIT } from '../limits'
import type { ConversationId, CreateRoomInput, Room, UserId } from '../types'

import { mockError } from './mockErrors'
import { type MockRoom, type MockStore, nextMockId, previewOf } from './mockHistories'

const ROOM_NAME_MIN_LENGTH = 3
const ROOM_NAME_MAX_LENGTH = 80
const ROOM_DESCRIPTION_MAX_LENGTH = 300
const OWNED_ROOM_LIMIT = 20

function toRoom(store: MockStore, room: MockRoom, withPreview: boolean): Room {
  return {
    kind: 'room',
    id: room.id,
    name: room.name,
    description: room.description,
    memberCount: room.memberIds.size,
    lastMessage: withPreview ? previewOf(store, room.id) : null,
  }
}

export function listMyRooms(store: MockStore, userId: UserId): Room[] {
  return [...store.rooms.values()]
    .filter((room) => room.memberIds.has(userId))
    .map((room) => toRoom(store, room, true))
}

/** Rooms the caller is not in, matched by name. The preview is always null here. */
export function discoverRooms(
  store: MockStore,
  userId: UserId,
  query: string,
  limit: number,
): Room[] {
  const needle = query.trim().toLowerCase()
  return [...store.rooms.values()]
    .filter((room) => !room.memberIds.has(userId))
    .filter((room) => room.name.toLowerCase().includes(needle))
    .slice(0, Math.min(limit, DISCOVER_MAX_LIMIT))
    .map((room) => toRoom(store, room, false))
}

export function createRoom(store: MockStore, ownerId: UserId, input: CreateRoomInput): Room {
  const name = input.name.trim()
  const description = input.description.trim()
  if (name.length < ROOM_NAME_MIN_LENGTH || name.length > ROOM_NAME_MAX_LENGTH) {
    throw mockError('invalid_room_name')
  }
  if (description.length > ROOM_DESCRIPTION_MAX_LENGTH) {
    throw mockError('invalid_room_description')
  }

  const rooms = [...store.rooms.values()]
  const lowerName = name.toLowerCase()
  if (rooms.some((room) => room.name.toLowerCase() === lowerName)) {
    throw mockError('room_name_taken')
  }
  if (rooms.filter((room) => room.ownerId === ownerId).length >= OWNED_ROOM_LIMIT) {
    throw mockError('room_limit_reached')
  }

  const room: MockRoom = {
    id: nextMockId(store, 'room'),
    name,
    description,
    ownerId,
    memberIds: new Set([ownerId]),
  }
  store.rooms.set(room.id, room)
  return toRoom(store, room, false)
}

/** Idempotent: joining a room the caller is already in changes nothing. */
export function joinRoom(store: MockStore, userId: UserId, roomId: ConversationId): void {
  const room = store.rooms.get(roomId)
  if (!room) throw mockError('not_found')
  room.memberIds.add(userId)
}

/** The last member leaving deletes the room and its history. */
export function leaveRoom(store: MockStore, userId: UserId, roomId: ConversationId): void {
  const room = store.rooms.get(roomId)
  if (!room || !room.memberIds.has(userId)) throw mockError('not_found')
  if (room.memberIds.size === 1) {
    store.rooms.delete(roomId)
    store.histories.delete(roomId)
    return
  }
  if (room.ownerId === userId) throw mockError('owner_cannot_leave')
  room.memberIds.delete(userId)
}
