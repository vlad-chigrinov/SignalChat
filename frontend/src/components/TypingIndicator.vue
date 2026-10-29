<script setup lang="ts">
/**
 * Always rendered, even when empty: the line keeps its height so typing
 * starting or stopping does not move the composer (no layout shift).
 */
defineProps<{ text: string }>()
</script>

<template>
  <p class="typing" role="status" aria-live="polite">{{ text }}</p>
</template>

<style scoped>
.typing {
  min-height: 1.25rem;
  margin: 0;
  padding: 0 16px;
  color: var(--color-muted);
  font-size: 0.8rem;
}
</style>
