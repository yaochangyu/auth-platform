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

export function useAuth() {
  const isSubmitting = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const result = ref<RegisterResponse | null>(null)
  const loginResult = ref<LoginResponse | null>(null)

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

  return { isSubmitting, error, result, loginResult, register, login, logout }
}
