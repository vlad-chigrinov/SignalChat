<script setup lang="ts">
import { computed } from 'vue'
import { RouterView, useRoute } from 'vue-router'

import ChatListPanel from '@/components/ChatListPanel.vue'
import { useLayout } from '@/composables/useLayout'
import { ROUTE_NAMES } from '@/router/routes'

const route = useRoute()
const { hasSidebar } = useLayout()

const isListRoute = computed(() => route.name === ROUTE_NAMES.chatList)
/** Wide screens always show the list; narrow ones show either the list or the open chat. */
const showList = computed(() => hasSidebar.value || isListRoute.value)
const showContent = computed(() => hasSidebar.value || !isListRoute.value)
</script>

<template>
  <div class="shell" :class="{ wide: hasSidebar }">
    <ChatListPanel v-if="showList" class="list" />
    <RouterView v-if="showContent" class="content" />
  </div>
</template>

<style scoped>
.shell {
  display: flex;
  height: 100%;
  min-height: 0;
}

.list {
  flex: 1;
  min-width: 0;
}

.wide .list {
  flex: none;
  width: var(--sidebar-width);
  border-right: 1px solid var(--color-border);
}

.content {
  flex: 1;
  min-width: 0;
}
</style>
