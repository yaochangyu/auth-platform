import { ref } from 'vue'
import type {
  ForgotPasswordRequest,
  ForgotPasswordResponse,
  ProblemDetails,
  ResetPasswordRequest,
  ResetPasswordResponse,
} from '@/types/auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

export function usePasswordReset() {
  const isSubmitting = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const forgotPasswordResult = ref<ForgotPasswordResponse | null>(null)
  const resetPasswordResult = ref<ResetPasswordResponse | null>(null)

  async function forgotPassword(payload: ForgotPasswordRequest) {
    isSubmitting.value = true
    error.value = null
    forgotPasswordResult.value = null

    try {
      const response = await fetch(`${API_BASE}/api/v1/auth/forgot-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })
      const data = await response.json()

      if (response.ok) {
        forgotPasswordResult.value = data as ForgotPasswordResponse
      } else {
        error.value = data as ProblemDetails
      }

      return response.ok
    } finally {
      isSubmitting.value = false
    }
  }

  async function resetPassword(payload: ResetPasswordRequest) {
    isSubmitting.value = true
    error.value = null
    resetPasswordResult.value = null

    try {
      const response = await fetch(`${API_BASE}/api/v1/auth/reset-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })
      const data = await response.json()

      if (response.ok) {
        resetPasswordResult.value = data as ResetPasswordResponse
      } else {
        error.value = data as ProblemDetails
      }

      return response.ok
    } finally {
      isSubmitting.value = false
    }
  }

  return {
    isSubmitting,
    error,
    forgotPasswordResult,
    resetPasswordResult,
    forgotPassword,
    resetPassword,
  }
}
