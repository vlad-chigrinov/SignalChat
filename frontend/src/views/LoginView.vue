<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { USE_MOCK_API } from '@/api'
import { useAuthStore } from '@/stores/auth'
import { errorMessage } from '@/utils/errorMessage'
import { safeReturnPath } from '@/utils/safeReturnPath'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const isStarting = ref(false)
const error = ref<string | null>(null)
const returnPath = computed(() => safeReturnPath(route.query.redirect))

async function signIn(): Promise<void> {
  isStarting.value = true
  error.value = null
  try {
    const signedInHere = await auth.login(returnPath.value)
    if (signedInHere) await router.replace(returnPath.value)
  } catch (caught) {
    error.value = errorMessage(caught)
    isStarting.value = false
  }
}
</script>

<template>
  <main class="login">
    <section class="card" aria-labelledby="login-title">
      <h1 id="login-title">SignalChat</h1>
      <p class="lead">Комнаты и личные сообщения в реальном времени.</p>
      <button type="button" class="button google" :disabled="isStarting" @click="signIn">
        {{ isStarting ? 'Переходим к Google…' : 'Войти через Google' }}
      </button>
      <p v-if="error ?? auth.error" class="error" role="alert">{{ error ?? auth.error }}</p>
      <p v-if="USE_MOCK_API" class="hint">Режим моков: вход выполняется без Google.</p>
    </section>
  </main>
</template>

<style scoped>
.login {
  display: grid;
  place-items: center;
  padding: 16px;
}

.card {
  width: min(100%, 360px);
  padding: 32px 24px;
  border: 1px solid var(--color-border);
  border-radius: 16px;
  background: var(--color-surface);
  text-align: center;
}

h1 {
  margin: 0 0 8px;
}

.lead {
  margin: 0 0 24px;
  color: var(--color-muted);
}

.google {
  width: 100%;
}

.error {
  margin: 16px 0 0;
  color: var(--color-danger);
}

.hint {
  margin: 16px 0 0;
  color: var(--color-muted);
  font-size: 0.8rem;
}
</style>
