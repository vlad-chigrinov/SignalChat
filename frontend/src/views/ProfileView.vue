<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'

import UserAvatar from '@/components/UserAvatar.vue'
import { ROUTE_NAMES } from '@/router/routes'
import { useAuthStore } from '@/stores/auth'

const PROFILE_AVATAR_SIZE_PX = 72

const auth = useAuthStore()
const router = useRouter()
const isLoggingOut = ref(false)

async function logout(): Promise<void> {
  isLoggingOut.value = true
  try {
    await auth.logout()
  } finally {
    await router.replace({ name: ROUTE_NAMES.login })
  }
}
</script>

<template>
  <main class="profile">
    <section v-if="auth.user" class="card" aria-labelledby="profile-title">
      <UserAvatar :user="auth.user" :size="PROFILE_AVATAR_SIZE_PX" />
      <h1 id="profile-title">{{ auth.user.displayName }}</h1>
      <p class="email">{{ auth.user.email }}</p>
      <button type="button" class="button secondary" :disabled="isLoggingOut" @click="logout">
        {{ isLoggingOut ? 'Выходим…' : 'Выйти' }}
      </button>
    </section>
  </main>
</template>

<style scoped>
.profile {
  padding: 16px;
}

.card {
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-items: center;
  width: min(100%, 360px);
  margin: 32px auto 0;
  padding: 32px 24px;
  border: 1px solid var(--color-border);
  border-radius: 16px;
  background: var(--color-surface);
}

h1 {
  margin: 8px 0 0;
  font-size: 1.25rem;
}

.email {
  margin: 0 0 16px;
  color: var(--color-muted);
  overflow-wrap: anywhere;
}
</style>
