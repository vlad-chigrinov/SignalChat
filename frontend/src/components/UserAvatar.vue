<script setup lang="ts">
import { computed } from 'vue'

import type { UserSummary } from '@/api'

const props = withDefaults(defineProps<{ user: UserSummary; size?: number }>(), { size: 36 })

const initials = computed(() =>
  props.user.displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join(''),
)
</script>

<template>
  <span class="avatar" :style="{ width: `${size}px`, height: `${size}px` }" aria-hidden="true">
    <img
      v-if="user.avatarUrl"
      :src="user.avatarUrl"
      alt=""
      loading="lazy"
      referrerpolicy="no-referrer"
    />
    <template v-else>{{ initials }}</template>
  </span>
</template>

<style scoped>
.avatar {
  display: inline-flex;
  flex: none;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  border-radius: 50%;
  background: var(--color-accent-soft);
  color: var(--color-accent);
  font-size: 0.8rem;
  font-weight: 600;
}

.avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
</style>
