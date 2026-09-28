import { ref } from 'vue'
import type { ProblemDetails, RegisterRequest, RegisterResponse } from '@/types/auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

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

  return { isSubmitting, error, result, register }
}
