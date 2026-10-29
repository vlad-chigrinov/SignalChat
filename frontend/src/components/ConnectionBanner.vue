<script setup lang="ts">
import { computed } from 'vue'

import { useConnectionStore } from '@/stores/connection'

const connection = useConnectionStore()

const text = computed(() =>
  connection.state === 'reconnecting'
    ? 'Переподключение…'
    : 'Нет соединения. Сообщения не отправляются, пока соединение не восстановится.',
)
</script>

<template>
  <p v-if="connection.isDegraded" class="banner" role="status" aria-live="polite">
    {{ text }}
  </p>
</template>

<style scoped>
.banner {
  margin: 0;
  padding: 6px 16px;
  background: var(--color-warning-soft);
  color: var(--color-warning);
  font-size: 0.875rem;
  text-align: center;
}
</style>
