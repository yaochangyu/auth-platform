import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import type { LocationQuery } from 'vue-router'
import { challengeOf, randomString } from '@/lib/pkce'

const AUTH_SERVER_URL = import.meta.env.VITE_AUTH_SERVER_URL ?? 'http://localhost:5102'
const TOKEN_URL = import.meta.env.VITE_TOKEN_URL ?? '/connect/token'
// 與 Token 端點一樣走同源代理，避免跨網域 CORS。
const USERINFO_URL = import.meta.env.VITE_USERINFO_URL ?? '/connect/userinfo'
const CLIENT_ID = 'developer-web'
const SCOPE = 'openid profile email developer_api'

const KEYS = { verifier: 'oauth.verifier', state: 'oauth.state', returnPath: 'oauth.returnPath' }

const LAST_LOGIN_KEY = 'oauth.lastLoginAt'
const RELOGIN_GUARD_MS = 30_000

const redirectUri = () => `${window.location.origin}/oauth/callback`

// 只接受站內相對路徑，避免登入後被導向外部網址（Open Redirect）。
const safeReturnPath = (path: string | null) => (path && path.startsWith('/') && !path.startsWith('//') ? path : '/apps')

export interface DeveloperProfile {
  sub: string
  email?: string
  nickname?: string
}

// 開發者後台認證狀態的單一真實來源。
// Access Token 只存在記憶體，不寫入 localStorage / sessionStorage，降低 XSS 竊取風險；
// 重新整理頁面後會經由 auth-server 的 SSO 無感重新取得。
export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(null)
  const expiresAt = ref(0)
  const profile = ref<DeveloperProfile | null>(null)
  const error = ref<string | null>(null)
  const isLoading = ref(false)

  const isAuthenticated = computed(() => accessToken.value !== null && Date.now() < expiresAt.value - 10_000)
  const displayIdentity = computed(() => profile.value?.email ?? profile.value?.nickname ?? profile.value?.sub ?? '')

  function setToken(token: string, expiresInSeconds: number) {
    accessToken.value = token
    expiresAt.value = Date.now() + expiresInSeconds * 1000
  }

  function clear() {
    accessToken.value = null
    expiresAt.value = 0
    profile.value = null
    error.value = null
  }

  // 身分資訊只是顯示用：取不到不影響已完成的登入，profile 維持 null。
  async function fetchProfile(): Promise<void> {
    isLoading.value = true
    try {
      const response = await fetch(USERINFO_URL, { headers: { Authorization: `Bearer ${accessToken.value}` } })
      profile.value = response.ok ? ((await response.json()) as DeveloperProfile) : null
    } catch {
      profile.value = null
    } finally {
      isLoading.value = false
    }
  }

  // Dogfooding：以自身 auth-server 的 Authorization Code + PKCE 登入。已有 SSO Session 時會無感回跳。
  async function startLogin(returnPath: string) {
    const verifier = randomString()
    const state = randomString(16)
    sessionStorage.setItem(KEYS.verifier, verifier)
    sessionStorage.setItem(KEYS.state, state)
    sessionStorage.setItem(KEYS.returnPath, returnPath)

    const params = new URLSearchParams({
      client_id: CLIENT_ID,
      response_type: 'code',
      redirect_uri: redirectUri(),
      scope: SCOPE,
      state,
      code_challenge: await challengeOf(verifier),
      code_challenge_method: 'S256',
    })
    window.location.assign(`${AUTH_SERVER_URL}/connect/authorize?${params}`)
  }

  // 成功回傳登入前的站內路徑；失敗回傳 null 並設定 error。
  async function completeLogin(query: LocationQuery): Promise<string | null> {
    const verifier = sessionStorage.getItem(KEYS.verifier)
    const expectedState = sessionStorage.getItem(KEYS.state)
    const returnPath = sessionStorage.getItem(KEYS.returnPath)
    Object.values(KEYS).forEach((key) => sessionStorage.removeItem(key))
    error.value = null

    if (query.error) {
      error.value = String(query.error_description ?? query.error)
      return null
    }

    if (!verifier || !expectedState || query.state !== expectedState || typeof query.code !== 'string') {
      error.value = '登入狀態不一致，請重新登入。'
      return null
    }

    const response = await fetch(TOKEN_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'authorization_code',
        code: query.code,
        client_id: CLIENT_ID,
        redirect_uri: redirectUri(),
        code_verifier: verifier,
      }),
    })

    if (!response.ok) {
      error.value = '無法取得存取權杖，請重新登入。'
      return null
    }

    const token = (await response.json()) as { access_token: string; expires_in: number }
    setToken(token.access_token, token.expires_in)
    await fetchProfile()
    sessionStorage.setItem(LAST_LOGIN_KEY, String(Date.now()))
    return safeReturnPath(returnPath)
  }

  // 剛完成登入卻又被 API 回 401，代表重新登入也無法解決（例如 Token 不被 API 接受），此時不能再導向登入，否則會無限迴圈。
  const justLoggedIn = () => Date.now() - Number(sessionStorage.getItem(LAST_LOGIN_KEY) ?? 0) < RELOGIN_GUARD_MS

  return {
    accessToken,
    profile,
    error,
    isLoading,
    isAuthenticated,
    displayIdentity,
    setToken,
    clear,
    fetchProfile,
    startLogin,
    completeLogin,
    justLoggedIn,
  }
})
