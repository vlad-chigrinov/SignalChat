<script setup lang="ts">
import { USE_MOCK_API } from '@/api'
import { ROUTE_NAMES } from '@/router/routes'
import { useAuthStore } from '@/stores/auth'

import UserAvatar from './UserAvatar.vue'

const auth = useAuthStore()
</script>

<template>
  <header class="header">
    <RouterLink class="brand" :to="{ name: ROUTE_NAMES.chatList }">SignalChat</RouterLink>
    <span v-if="USE_MOCK_API" class="badge" title="Данные из frontend/src/api/mock">моки</span>
    <nav class="nav" aria-label="Основная навигация">
      <RouterLink :to="{ name: ROUTE_NAMES.chatList }">Чаты</RouterLink>
      <RouterLink v-if="auth.user" class="profile" :to="{ name: ROUTE_NAMES.profile }">
        <UserAvatar :user="auth.user" :size="28" />
        <span class="sr-only">Профиль</span>
      </RouterLink>
    </nav>
  </header>
</template>

<style scoped>
.header {
  display: flex;
  gap: 12px;
  align-items: center;
  height: var(--header-height);
  padding: 0 16px;
  border-bottom: 1px solid var(--color-border);
  background: var(--color-surface);
}

.brand {
  color: inherit;
  font-weight: 700;
  text-decoration: none;
}

.badge {
  padding: 2px 8px;
  border-radius: 999px;
  background: var(--color-warning-soft);
  color: var(--color-warning);
  font-size: 0.75rem;
}

.nav {
  display: flex;
  gap: 16px;
  align-items: center;
  margin-left: auto;
}

.nav a {
  color: var(--color-muted);
  text-decoration: none;
}

.nav a.router-link-exact-active {
  color: var(--color-text);
}

.profile {
  display: inline-flex;
}
</style>
