import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

export interface CurrentUserResponse {
  isAuthenticated: boolean
  id?: string | null
  login?: string | null
  name?: string | null
  avatarUrl?: string | null
  htmlUrl?: string | null
  organization?: string | null
}

const anonymousUser: CurrentUserResponse = {
  isAuthenticated: false,
  id: null,
  login: null,
  name: null,
  avatarUrl: null,
  htmlUrl: null,
  organization: null,
}

function getCurrentLocalPath() {
  return `${window.location.pathname}${window.location.search}${window.location.hash}`
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<CurrentUserResponse>(anonymousUser)
  const loaded = ref(false)
  const loading = ref(false)

  const isAuthenticated = computed(() => user.value.isAuthenticated)

  async function loadCurrentUser(force = false) {
    if (loaded.value && !force) {
      return user.value
    }

    loading.value = true
    try {
      const response = await fetch('/api/v1/auth/me', {
        credentials: 'same-origin',
      })

      if (response.status === 401 || response.status === 403) {
        user.value = anonymousUser
        loaded.value = true
        return user.value
      }

      if (!response.ok) {
        throw new Error(await response.text() || `${response.status} ${response.statusText}`)
      }

      user.value = await response.json() as CurrentUserResponse
      loaded.value = true
      return user.value
    } finally {
      loading.value = false
    }
  }

  function login(returnUrl = getCurrentLocalPath()) {
    window.location.assign(`/api/v1/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`)
  }

  async function logout() {
    const response = await fetch('/api/v1/auth/logout', {
      method: 'POST',
      credentials: 'same-origin',
    })

    if (!response.ok && response.status !== 401 && response.status !== 403) {
      throw new Error(await response.text() || `${response.status} ${response.statusText}`)
    }

    user.value = anonymousUser
    loaded.value = true
    window.location.assign('/auth/login')
  }

  return {
    user,
    loaded,
    loading,
    isAuthenticated,
    loadCurrentUser,
    login,
    logout,
  }
})
