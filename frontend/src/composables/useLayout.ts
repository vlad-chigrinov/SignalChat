import { useMediaQuery } from './useMediaQuery'
import {
  PARTICIPANTS_MIN_WIDTH_PX,
  SIDEBAR_MIN_WIDTH_PX,
  minWidthQuery,
} from '@/utils/layoutBreakpoints'

/** What the screen has room for: the chat list beside the chat, and the participants beside the conversation. */
export function useLayout() {
  return {
    hasSidebar: useMediaQuery(minWidthQuery(SIDEBAR_MIN_WIDTH_PX)),
    hasParticipantsPanel: useMediaQuery(minWidthQuery(PARTICIPANTS_MIN_WIDTH_PX)),
  }
}
