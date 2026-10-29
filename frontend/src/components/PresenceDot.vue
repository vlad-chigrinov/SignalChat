<script setup lang="ts">
defineProps<{ online: boolean }>()
</script>

<template>
  <span
    class="presence"
    :class="{ online }"
    role="img"
    :aria-label="online ? 'В сети' : 'Не в сети'"
  ></span>
</template>

<style scoped>
.presence {
  display: inline-block;
  flex: none;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--color-muted);
}

.presence.online {
  background: var(--color-online);
}
</style>
