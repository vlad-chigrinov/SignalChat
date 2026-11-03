import type { UserSummary } from '@/api'

import { useDirectRequestsStore } from './directRequests'
import { useMessagesStore } from './messages'
import { useRoomMembersStore } from './roomMembers'
import { useRoomNoticesStore } from './roomNotices'
import { useRoomsStore } from './rooms'
import { useUserSearchStore } from './userSearch'

/**
 * A user changed their name or avatar: every list that shows them shows the new profile. Used for the live event
 * and for the signed-in user's own change, so the same places update in both cases. The signed-in user's own profile
 * (header, profile page) lives in the auth store, which takes the change itself.
 */
export function applyProfileChange(user: UserSummary): void {
  useRoomsStore().applyProfileChange(user)
  useDirectRequestsStore().applyProfileChange(user)
  useMessagesStore().applyProfileChange(user)
  useUserSearchStore().applyProfileChange(user)
  useRoomMembersStore().applyProfileChange(user)
  useRoomNoticesStore().applyProfileChange(user)
}
