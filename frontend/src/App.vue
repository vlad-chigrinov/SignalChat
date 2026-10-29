<script setup lang="ts">
import { RouterView } from 'vue-router'

import AppHeader from '@/components/AppHeader.vue'
import { useSessionBootstrap } from '@/composables/useSessionBootstrap'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
useSessionBootstrap()
</script>

<template>
  <div class="app">
    <AppHeader v-if="auth.isAuthenticated" />
    <RouterView class="page" />
  </div>
</template>

<style scoped>
.app {
  display: flex;
  flex-direction: column;
  height: 100dvh;
}

.page {
  flex: 1;
  min-height: 0;
}
</style>
