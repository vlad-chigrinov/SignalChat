import { ref } from 'vue'

import type { DirectConversation, UserId } from '@/api'
import { useRoomsStore } from '@/stores/rooms'
import { errorMessage } from '@/utils/errorMessage'

/**
 * Sends the first message to a user as a request (or opens the chat that exists), for the dialogs that start a
 * conversation. `send` answers the conversation, or null after showing the error in `sendError`.
 */
export function useDirectRequestSend() {
  const rooms = useRoomsStore()
  const isSending = ref(false)
  const sendError = ref<string | null>(null)

  async function send(userId: UserId, text: string): Promise<DirectConversation | null> {
    sendError.value = null
    isSending.value = true
    try {
      return await rooms.sendDirectRequest(userId, text)
    } catch (error) {
      sendError.value = errorMessage(error)
      return null
    } finally {
      isSending.value = false
    }
  }

  function clearError(): void {
    sendError.value = null
  }

  return { isSending, sendError, send, clearError }
}
