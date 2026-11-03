<script setup lang="ts">
import type { RoomMember } from '@/api'

import PresenceDot from './PresenceDot.vue'
import UserAvatar from './UserAvatar.vue'

defineProps<{ member: RoomMember; isSelf: boolean; isOnline: boolean }>()
const emit = defineEmits<{ write: [] }>()
</script>

<template>
  <div class="member">
    <UserAvatar :user="member.user" :size="36" />
    <div class="info">
      <p class="name">
        {{ member.user.displayName }}
        <span v-if="isSelf" class="tag">(вы)</span>
        <span v-if="member.role === 'owner'" class="tag">владелец</span>
      </p>
      <p class="presence">
        <PresenceDot :online="isOnline" />
        {{ isOnline ? 'в сети' : 'не в сети' }}
      </p>
    </div>
    <button
      v-if="!isSelf"
      type="button"
      class="button secondary"
      :aria-label="`Написать: ${member.user.displayName}`"
      @click="emit('write')"
    >
      Написать
    </button>
  </div>
</template>

<style scoped>
.member {
  display: flex;
  gap: 12px;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid var(--color-border);
}

.info {
  flex: 1;
  min-width: 0;
}

.info p {
  margin: 0;
  overflow-wrap: anywhere;
}

.name {
  font-weight: 600;
}

.tag {
  color: var(--color-muted);
  font-size: 0.8rem;
  font-weight: 400;
}

.presence {
  display: flex;
  gap: 6px;
  align-items: center;
  color: var(--color-muted);
  font-size: 0.8rem;
}
</style>
