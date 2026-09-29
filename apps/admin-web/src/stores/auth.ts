import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

// Access Token 只存在記憶體，不寫入 localStorage / sessionStorage，降低 XSS 竊取風險；
// 重新整理頁面後會經由 auth-server 的 SSO 無感重新取得。
export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(null)
  const expiresAt = ref(0)

  const isAuthenticated = computed(() => accessToken.value !== null && Date.now() < expiresAt.value - 10_000)

  function setToken(token: string, expiresInSeconds: number) {
    accessToken.value = token
    expiresAt.value = Date.now() + expiresInSeconds * 1000
  }

  function clear() {
    accessToken.value = null
    expiresAt.value = 0
  }

  return { accessToken, isAuthenticated, setToken, clear }
})
