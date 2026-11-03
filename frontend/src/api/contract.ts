import type {
  ConversationId,
  CreateRoomInput,
  CurrentUser,
  CursorPage,
  DirectConversation,
  HistoryQuery,
  IncomingDirectRequest,
  LoginOutcome,
  Message,
  MessageId,
  Reaction,
  Room,
  RoomMember,
  UserId,
  UserSummary,
} from './types'

/** Everything the UI needs from the backend. Implemented by the HTTP client and by the mocks. */
export interface ChatApi {
  /** Null when the visitor has no valid session (HTTP 401). */
  getCurrentUser(): Promise<CurrentUser | null>
  /** Starts Google sign-in. The real client leaves the page; the mock signs in locally. */
  beginLogin(returnPath: string): Promise<LoginOutcome>
  logout(): Promise<void>
  /** Rooms the caller is a member of. */
  listRooms(): Promise<Room[]>
  /** Rooms the caller is not a member of, matched by name. */
  discoverRooms(query: string, limit: number): Promise<Room[]>
  createRoom(input: CreateRoomInput): Promise<Room>
  /** Idempotent. */
  joinRoom(roomId: ConversationId): Promise<void>
  leaveRoom(roomId: ConversationId): Promise<void>
  /** Members of a room the caller is in (the owner first). `not_found` for anyone else. */
  listRoomMembers(roomId: ConversationId): Promise<RoomMember[]>
  /**
   * Adds a chat partner of the caller to the room, with no confirmation. Rejects with `not_found` (the caller is
   * not in the room, or has no accepted chat with the user) and `already_member`.
   */
  inviteToRoom(roomId: ConversationId, userId: UserId): Promise<void>
  /** The caller's accepted chats, plus their own pending requests (status `pending`). */
  listDirectConversations(): Promise<DirectConversation[]>
  /**
   * Writes the first message to a user: a request, unless the two already chat. The answer's
   * `status` tells which: `accepted` (the existing chat) or `pending` (the request).
   */
  sendDirectRequest(userId: UserId, text: string): Promise<DirectConversation>
  /** Requests waiting for the caller's answer. */
  listIncomingRequests(): Promise<IncomingDirectRequest[]>
  acceptRequest(conversationId: ConversationId): Promise<void>
  declineRequest(conversationId: ConversationId): Promise<void>
  /** Other users matched by display name. Never carries an email. */
  searchUsers(query: string, limit: number): Promise<UserSummary[]>
  /**
   * Sends a message of 1..9 photos with an optional caption, as one multipart request. The photos leave the browser
   * only here, never when they are picked. The server broadcasts the message over the hub like any other and answers
   * with it. Rejects with `invalid_attachments`, `invalid_image`, `attachment_too_large` or `storage_unavailable`.
   */
  sendPhotoMessage(
    conversationId: ConversationId,
    caption: string,
    photos: readonly File[],
  ): Promise<Message>
  /** History only. Text messages are sent through the realtime hub (C1), photo messages through sendPhotoMessage. */
  getMessages(conversationId: ConversationId, query: HistoryQuery): Promise<CursorPage<Message>>
  /**
   * The caller picks a reaction for a message: none yet → added, another → replaced, the same →
   * removed. Answers with the message's reactions as the caller sees them. Rejects with
   * `invalid_reaction`, `not_found`, `dm_not_accepted` or `rate_limited`.
   */
  chooseReaction(
    conversationId: ConversationId,
    messageId: MessageId,
    emoji: string,
  ): Promise<Reaction[]>
  getOnlineUserIds(): Promise<UserId[]>
  /** The caller's own name. Rejects with `invalid_display_name`. Answers with the updated profile. */
  changeDisplayName(displayName: string): Promise<CurrentUser>
  /**
   * The caller's own avatar: the picture is sent as it is and the server re-encodes it. Rejects with
   * `invalid_avatar` or `avatar_too_large`. Answers with the updated profile.
   */
  uploadAvatar(file: Blob): Promise<CurrentUser>
  /** Drops the uploaded avatar; the profile shows the Google one again. */
  resetAvatar(): Promise<CurrentUser>
}
