<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import { AVATAR_ALLOWED_TYPES, DISPLAY_NAME_MAX_LENGTH } from '@/api/limits'
import UserAvatar from '@/components/UserAvatar.vue'
import { ROUTE_NAMES } from '@/router/routes'
import { useAuthStore } from '@/stores/auth'
import { errorMessage } from '@/utils/errorMessage'
import { avatarFileError, displayNameError, isUploadedAvatar } from '@/utils/profileForm'

const PROFILE_AVATAR_SIZE_PX = 72
const AVATAR_ACCEPT = AVATAR_ALLOWED_TYPES.join(',')

const auth = useAuthStore()
const router = useRouter()
const isLoggingOut = ref(false)

const name = ref(auth.user?.displayName ?? '')
const isSavingName = ref(false)
const nameError = ref<string | null>(null)
const nameSaved = ref(false)

const isChangingAvatar = ref(false)
const avatarError = ref<string | null>(null)

const hasUploadedAvatar = computed(() => isUploadedAvatar(auth.user?.avatarUrl ?? null))
const isNameChanged = computed(() => name.value.trim() !== auth.user?.displayName)

// The name can also change from another tab: the field follows it unless the user is in the middle of typing.
watch(
  () => auth.user?.displayName,
  (current, previous) => {
    if (current !== undefined && name.value === previous) name.value = current
  },
)

async function saveName(): Promise<void> {
  nameSaved.value = false
  nameError.value = displayNameError(name.value)
  if (nameError.value !== null || isSavingName.value || !isNameChanged.value) return

  isSavingName.value = true
  try {
    await auth.changeDisplayName(name.value)
    name.value = auth.user?.displayName ?? name.value
    nameSaved.value = true
  } catch (error) {
    nameError.value = errorMessage(error)
  } finally {
    isSavingName.value = false
  }
}

async function pickAvatar(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  // The same file can be picked again after an error.
  input.value = ''
  if (!file) return

  avatarError.value = avatarFileError(file)
  if (avatarError.value !== null) return
  await changeAvatar(() => auth.uploadAvatar(file))
}

async function changeAvatar(change: () => Promise<void>): Promise<void> {
  if (isChangingAvatar.value) return
  isChangingAvatar.value = true
  avatarError.value = null
  try {
    await change()
  } catch (error) {
    avatarError.value = errorMessage(error)
  } finally {
    isChangingAvatar.value = false
  }
}

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

      <div class="avatar-actions">
        <label class="button secondary" :aria-disabled="isChangingAvatar">
          Загрузить фото
          <input
            class="sr-only"
            type="file"
            :accept="AVATAR_ACCEPT"
            :disabled="isChangingAvatar"
            aria-label="Файл с фото"
            @change="pickAvatar"
          />
        </label>
        <button
          v-if="hasUploadedAvatar"
          type="button"
          class="button secondary"
          :disabled="isChangingAvatar"
          @click="changeAvatar(() => auth.resetAvatar())"
        >
          Фото из Google
        </button>
      </div>
      <p v-if="avatarError" class="error" role="alert">{{ avatarError }}</p>

      <form class="name-form" novalidate @submit.prevent="saveName">
        <label class="field">
          <span>Имя</span>
          <input
            v-model="name"
            class="input"
            type="text"
            autocomplete="off"
            :maxlength="DISPLAY_NAME_MAX_LENGTH * 2"
            :aria-invalid="nameError !== null"
            aria-describedby="profile-name-error"
            @input="nameSaved = false"
          />
        </label>
        <p v-if="nameError" id="profile-name-error" class="error" role="alert">{{ nameError }}</p>
        <p v-else-if="nameSaved" class="saved" role="status">Имя сохранено.</p>
        <button type="submit" class="button" :disabled="isSavingName || !isNameChanged">
          {{ isSavingName ? 'Сохраняем…' : 'Сохранить имя' }}
        </button>
      </form>

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
  overflow-wrap: anywhere;
  text-align: center;
}

.email {
  margin: 0 0 16px;
  color: var(--color-muted);
  overflow-wrap: anywhere;
}

.avatar-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: center;
}

/* The file input sits inside the label, so the label is the visible, keyboard-reachable button. */
.avatar-actions label:focus-within {
  outline: 2px solid var(--color-accent);
  outline-offset: 2px;
}

.name-form {
  display: grid;
  gap: 8px;
  width: 100%;
  margin: 16px 0;
}

.field {
  display: grid;
  gap: 6px;
}

.input {
  padding: 8px 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg);
  color: inherit;
  font: inherit;
}

.input[aria-invalid='true'] {
  border-color: var(--color-danger);
}

.error {
  margin: 0;
  color: var(--color-danger);
  font-size: 0.9rem;
}

.saved {
  margin: 0;
  color: var(--color-online);
  font-size: 0.9rem;
}
</style>
