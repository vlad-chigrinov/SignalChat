<script setup lang="ts">
import BaseDialog from '@/components/BaseDialog.vue'
import RoomMembersList from '@/components/RoomMembersList.vue'
import type { ConversationId } from '@/api'

defineProps<{ roomId: ConversationId }>()
const emit = defineEmits<{ close: []; opened: [conversationId: ConversationId] }>()
</script>

<template>
  <BaseDialog title="Участники" @close="emit('close')">
    <RoomMembersList :room-id="roomId" @opened="emit('opened', $event)" />
  </BaseDialog>
</template>
