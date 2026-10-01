import { defineStore } from 'pinia'
import type {
  LoginRequest,
  LoginResponse,
  LogoutResponse,
  MemberProfileResponse,
  ProblemDetails,
  RegisterRequest,
  RegisterResponse,
} from '@/types/auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

// 認證相關狀態的單一真實來源：登入結果、會員 Profile 與表單回饋都在這裡，登出時由 $reset() 一次清空。
export const useAuthStore = defineStore('auth', {
  state: () => ({
    profile: null as MemberProfileResponse | null,
    isLoading: false,
    isSubmitting: false,
    error: null as ProblemDetails | null,
    registerResult: null as RegisterResponse | null,
    loginResult: null as LoginResponse | null,
  }),

  getters: {
    isAuthenticated: (state) => state.profile !== null,
  },

  actions: {
    // 回傳是否成功，供 Router Auth Guard 判斷要不要放行。
    async fetchProfile(): Promise<boolean> {
      this.isLoading = true

      try {
        const response = await fetch(`${API_BASE}/api/v1/member/profile`, {
          credentials: 'include',
        })

        if (response.ok) {
          this.profile = (await response.json()) as MemberProfileResponse
          return true
        }

        this.profile = null
        return false
      } finally {
        this.isLoading = false
      }
    },

    async register(payload: RegisterRequest): Promise<void> {
      this.isSubmitting = true
      this.error = null
      this.registerResult = null

      try {
        const response = await fetch(`${API_BASE}/api/v1/auth/register`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload),
        })
        const data = await response.json()

        if (response.ok) {
          this.registerResult = data as RegisterResponse
        } else {
          this.error = data as ProblemDetails
        }
      } finally {
        this.isSubmitting = false
      }
    },

    async login(payload: LoginRequest): Promise<boolean> {
      this.isSubmitting = true
      this.error = null
      this.loginResult = null

      try {
        const response = await fetch(`${API_BASE}/api/v1/auth/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify(payload),
        })
        const data = await response.json()

        if (response.ok) {
          this.loginResult = data as LoginResponse
        } else {
          this.error = data as ProblemDetails
        }

        return response.ok
      } finally {
        this.isSubmitting = false
      }
    },

    // 安全關鍵：轉址一律使用後端回傳、已校驗過的 returnUrl，嚴禁直接採用前端未校驗的 query 字串。
    // returnUrl 的讀取封裝在此，呼叫端（元件）不需要也不應該知道 query 參數的存在。
    async loginAndRedirect(credentials: Pick<LoginRequest, 'email' | 'password'>): Promise<boolean> {
      const returnUrl = new URLSearchParams(window.location.search).get('returnUrl') ?? undefined
      const ok = await this.login({ ...credentials, returnUrl })
      if (ok) {
        window.location.assign(this.loginResult?.returnUrl ?? '/member')
      }
      return ok
    },

    // 無論後端回應成功、失敗或網路中斷，前端狀態都無條件清空。
    async logout(): Promise<LogoutResponse | null> {
      try {
        const response = await fetch(`${API_BASE}/api/v1/auth/logout`, {
          method: 'POST',
          credentials: 'include',
        })

        return response.ok ? ((await response.json()) as LogoutResponse) : null
      } catch {
        return null
      } finally {
        this.$reset()
      }
    },

    async logoutAndRedirect(): Promise<void> {
      await this.logout()
      window.location.assign('/login')
    },

    // 表單回饋（錯誤、註冊結果）是跨頁共用的狀態，換頁時清掉。
    clearFeedback() {
      this.error = null
      this.registerResult = null
    },
  },
})
