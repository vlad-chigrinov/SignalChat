import type {
  ConversationId,
  CurrentUser,
  CursorPage,
  DirectConversation,
  HistoryQuery,
  LoginOutcome,
  Message,
  Room,
  UserId,
} from './types'

/** Everything the UI needs from the backend. Implemented by the HTTP client and by the mocks. */
export interface ChatApi {
  /** Null when the visitor has no valid session (HTTP 401). */
  getCurrentUser(): Promise<CurrentUser | null>
  /** Starts Google sign-in. The real client leaves the page; the mock signs in locally. */
  beginLogin(returnPath: string): Promise<LoginOutcome>
  logout(): Promise<void>
  listRooms(): Promise<Room[]>
  listDirectConversations(): Promise<DirectConversation[]>
  /** History only. Messages are sent through the realtime hub (C1), never over REST. */
  getMessages(conversationId: ConversationId, query: HistoryQuery): Promise<CursorPage<Message>>
  getOnlineUserIds(): Promise<UserId[]>
}
