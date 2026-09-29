import { ref } from 'vue'
import type { ChangePasswordRequest, ChangePasswordResponse, ProblemDetails } from '@/types/auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

export function useChangePassword() {
  const isSubmitting = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const result = ref<ChangePasswordResponse | null>(null)

  async function changePassword(payload: ChangePasswordRequest) {
    isSubmitting.value = true
    error.value = null
    result.value = null

    try {
      const response = await fetch(`${API_BASE}/api/v1/member/password`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(payload),
      })
      const data = await response.json()

      if (response.ok) {
        result.value = data as ChangePasswordResponse
      } else {
        error.value = data as ProblemDetails
      }

      return response.ok
    } finally {
      isSubmitting.value = false
    }
  }

  return { isSubmitting, error, result, changePassword }
}
