import { ref } from 'vue'
import { defineStore } from 'pinia'

import type { ConversationId, UserId } from '@/api'
import { TYPING_INDICATOR_TIMEOUT_MS, type UserTypingEvent } from '@/realtime/hubContract'

import { useAuthStore } from './auth'

type Timer = ReturnType<typeof setTimeout>

const NOBODY: readonly UserId[] = Object.freeze([])

/**
 * Who is typing in each conversation right now. An entry lives for
 * TYPING_INDICATOR_TIMEOUT_MS unless the same user renews it or sends a message.
 */
export const useTypingStore = defineStore('typing', () => {
  const typingByConversation = ref<Record<ConversationId, UserId[]>>({})
  const timers = new Map<string, Timer>()

  function timerKey(conversationId: ConversationId, userId: UserId): string {
    return JSON.stringify([conversationId, userId])
  }

  function typingUserIds(conversationId: ConversationId): readonly UserId[] {
    return typingByConversation.value[conversationId] ?? NOBODY
  }

  function cancelTimer(conversationId: ConversationId, userId: UserId): void {
    const key = timerKey(conversationId, userId)
    const timer = timers.get(key)
    if (timer !== undefined) clearTimeout(timer)
    timers.delete(key)
  }

  function remove(conversationId: ConversationId, userId: UserId): void {
    cancelTimer(conversationId, userId)
    const users = typingByConversation.value[conversationId]
    if (!users?.includes(userId)) return
    typingByConversation.value[conversationId] = users.filter((id) => id !== userId)
  }

  /** Real-time entry point: someone is typing. The current user is never shown to themselves. */
  function applyUserTyping({ conversationId, userId }: UserTypingEvent): void {
    if (userId === useAuthStore().user?.id) return
    cancelTimer(conversationId, userId)
    const users = typingByConversation.value[conversationId] ?? []
    if (!users.includes(userId)) typingByConversation.value[conversationId] = [...users, userId]
    timers.set(
      timerKey(conversationId, userId),
      setTimeout(() => remove(conversationId, userId), TYPING_INDICATOR_TIMEOUT_MS),
    )
  }

  /** A message from this author means they stopped typing. */
  function clearUser(conversationId: ConversationId, userId: UserId): void {
    remove(conversationId, userId)
  }

  function clearConversation(conversationId: ConversationId): void {
    for (const userId of typingUserIds(conversationId)) remove(conversationId, userId)
  }

  function reset(): void {
    timers.forEach((timer) => clearTimeout(timer))
    timers.clear()
    typingByConversation.value = {}
  }

  return {
    typingByConversation,
    typingUserIds,
    applyUserTyping,
    clearUser,
    clearConversation,
    reset,
  }
})
