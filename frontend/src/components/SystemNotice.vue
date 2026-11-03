<script setup lang="ts">
import type { RoomNotice } from '@/utils/timeline'

defineProps<{ notice: RoomNotice }>()
</script>

<template>
  <p class="notice">{{ notice.user.displayName }} присоединился</p>
</template>

<style scoped>
.notice {
  align-self: center;
  margin: 0;
  padding: 2px 12px;
  border-radius: 12px;
  background: var(--color-accent-soft);
  color: var(--color-muted);
  font-size: 0.8rem;
  text-align: center;
  overflow-wrap: anywhere;
}
</style>
