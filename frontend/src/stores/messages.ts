import { ref } from 'vue'
import { defineStore } from 'pinia'

import { api, type ConversationId, type Message } from '@/api'
import { errorMessage } from '@/utils/errorMessage'
import { mergeMessages } from '@/utils/mergeMessages'

import { useRoomsStore } from './rooms'

export const MESSAGE_PAGE_SIZE = 30
export const MAX_MESSAGE_LENGTH = 2000

export interface ConversationHistory {
  items: Message[]
  nextCursor: string | null
  hasLoadedLatest: boolean
  isLoading: boolean
  error: string | null
}

function emptyHistory(): ConversationHistory {
  return { items: [], nextCursor: null, hasLoadedLatest: false, isLoading: false, error: null }
}

const EMPTY_HISTORY: Readonly<ConversationHistory> = Object.freeze(emptyHistory())

/** Message history per conversation, loaded page by page from the newest backwards. */
export const useMessagesStore = defineStore('messages', () => {
  const histories = ref<Record<ConversationId, ConversationHistory>>({})

  function historyOf(conversationId: ConversationId): Readonly<ConversationHistory> {
    return histories.value[conversationId] ?? EMPTY_HISTORY
  }

  function ensureHistory(conversationId: ConversationId): ConversationHistory {
    histories.value[conversationId] ??= emptyHistory()
    return histories.value[conversationId]
  }

  async function fetchPage(conversationId: ConversationId, before: string | null): Promise<void> {
    const history = ensureHistory(conversationId)
    history.isLoading = true
    try {
      const page = await api.getMessages(conversationId, { before, limit: MESSAGE_PAGE_SIZE })
      history.items = mergeMessages(history.items, page.items)
      history.nextCursor = page.nextCursor
      history.hasLoadedLatest = true
      history.error = null
    } catch (caught) {
      history.error = errorMessage(caught)
    } finally {
      history.isLoading = false
    }
  }

  /** First page (latest messages). No-op once loaded. */
  function loadLatest(conversationId: ConversationId): Promise<void> {
    const history = historyOf(conversationId)
    if (history.hasLoadedLatest || history.isLoading) return Promise.resolve()
    return fetchPage(conversationId, null)
  }

  /** Previous page, triggered by scrolling to the top. */
  function loadOlder(conversationId: ConversationId): Promise<void> {
    const history = historyOf(conversationId)
    if (!history.hasLoadedLatest || history.isLoading || history.nextCursor === null) {
      return Promise.resolve()
    }
    return fetchPage(conversationId, history.nextCursor)
  }

  /** Real-time entry point (SignalR, #7): a message was posted in a conversation. */
  function applyIncomingMessage(message: Message): void {
    const history = ensureHistory(message.conversationId)
    history.items = mergeMessages(history.items, [message])
    useRoomsStore().applyLastMessage(message)
  }

  async function send(conversationId: ConversationId, rawText: string): Promise<void> {
    const text = rawText.trim()
    if (text.length === 0 || text.length > MAX_MESSAGE_LENGTH) {
      throw new RangeError(`Message must be 1..${MAX_MESSAGE_LENGTH} characters`)
    }
    const message = await api.sendMessage(conversationId, text)
    applyIncomingMessage(message)
  }

  function reset(): void {
    histories.value = {}
  }

  return { histories, historyOf, loadLatest, loadOlder, applyIncomingMessage, send, reset }
})
