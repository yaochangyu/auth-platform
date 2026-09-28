import { ref } from 'vue'
import type {
  LoginRequest,
  LoginResponse,
  LogoutResponse,
  ProblemDetails,
  RegisterRequest,
  RegisterResponse,
} from '@/types/auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

// module-level：所有元件共用同一份登入會話狀態
const loginResult = ref<LoginResponse | null>(null)

export function useAuth() {
  const isSubmitting = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const result = ref<RegisterResponse | null>(null)

  async function register(payload: RegisterRequest) {
    isSubmitting.value = true
    error.value = null
    result.value = null

    try {
      const response = await fetch(`${API_BASE}/api/v1/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })
      const data = await response.json()

      if (response.ok) {
        result.value = data as RegisterResponse
      } else {
        error.value = data as ProblemDetails
      }
    } finally {
      isSubmitting.value = false
    }
  }

  async function login(payload: LoginRequest) {
    isSubmitting.value = true
    error.value = null
    loginResult.value = null

    try {
      const response = await fetch(`${API_BASE}/api/v1/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(payload),
      })
      const data = await response.json()

      if (response.ok) {
        loginResult.value = data as LoginResponse
      } else {
        error.value = data as ProblemDetails
      }

      return response.ok
    } finally {
      isSubmitting.value = false
    }
  }

  // 安全關鍵：轉址一律使用後端回傳、已校驗過的 returnUrl，嚴禁直接採用前端未校驗的 query 字串。
  async function loginAndRedirect(payload: LoginRequest) {
    const ok = await login(payload)
    if (ok && loginResult.value?.returnUrl) {
      window.location.assign(loginResult.value.returnUrl)
    }
    return ok
  }

  async function logout(): Promise<LogoutResponse | null> {
    const response = await fetch(`${API_BASE}/api/v1/auth/logout`, {
      method: 'POST',
      credentials: 'include',
    })

    if (!response.ok) {
      return null
    }

    return (await response.json()) as LogoutResponse
  }

  async function logoutAndRedirect() {
    await logout()
    loginResult.value = null
    window.location.assign('/login')
  }

  return {
    isSubmitting,
    error,
    result,
    loginResult,
    register,
    login,
    loginAndRedirect,
    logout,
    logoutAndRedirect,
  }
}
