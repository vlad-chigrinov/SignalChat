import { ref } from 'vue'
import { defineStore } from 'pinia'

import { api, type ConversationId, type CursorPage, type Message } from '@/api'
import { errorMessage } from '@/utils/errorMessage'
import { mergeMessages } from '@/utils/mergeMessages'

import { useRoomsStore } from './rooms'

// Sending lives in the realtime service (hub only, C1); the limit is shared from here.
export { MAX_MESSAGE_LENGTH } from '@/utils/messageText'

export const MESSAGE_PAGE_SIZE = 30
/** Upper bound on pages read in one catch-up: 150 messages, then the gap stays until reopened. */
const MAX_CATCH_UP_PAGES = 5

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

  /**
   * Re-reads the newest page after a reconnect and merges it. Older pages already loaded
   * stay, and the cursor for them is kept. Best effort: on failure the history is untouched.
   */
  /**
   * Catches up after a reconnect: reads pages back from the newest one until a page holds a
   * message already in the history (or the page cap is reached), so a longer outage is filled in.
   */
  async function refreshLatest(conversationId: ConversationId): Promise<void> {
    const history = ensureHistory(conversationId)
    const knownIds = new Set(history.items.map((message) => message.id))
    try {
      const latest = await api.getMessages(conversationId, {
        before: null,
        limit: MESSAGE_PAGE_SIZE,
      })
      const missed = await readMissedPages(conversationId, latest, knownIds)
      history.items = mergeMessages(history.items, missed)
      if (!history.hasLoadedLatest) history.nextCursor = latest.nextCursor
      history.hasLoadedLatest = true
    } catch {
      // The next reconnect or opening the chat tries again.
    }
  }

  /** Older pages, read from the newest page's cursor, until one of them reaches known messages. */
  async function readMissedPages(
    conversationId: ConversationId,
    newest: CursorPage<Message>,
    knownIds: ReadonlySet<string>,
  ): Promise<Message[]> {
    const collected = [...newest.items]
    if (knownIds.size === 0) return collected
    let page = newest
    for (let pagesRead = 1; pagesRead < MAX_CATCH_UP_PAGES; pagesRead += 1) {
      if (page.items.some((message) => knownIds.has(message.id))) break
      if (page.nextCursor === null) break
      page = await api.getMessages(conversationId, {
        before: page.nextCursor,
        limit: MESSAGE_PAGE_SIZE,
      })
      collected.unshift(...page.items)
    }
    return collected
  }

  /** Real-time entry point (SignalR, #7): a message was posted in a conversation. */
  function applyIncomingMessage(message: Message): void {
    const history = ensureHistory(message.conversationId)
    history.items = mergeMessages(history.items, [message])
    useRoomsStore().applyLastMessage(message)
  }

  function reset(): void {
    histories.value = {}
  }

  return {
    histories,
    historyOf,
    loadLatest,
    loadOlder,
    refreshLatest,
    applyIncomingMessage,
    reset,
  }
})
