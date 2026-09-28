import { ref } from 'vue'
import type { ProblemDetails, VerifyEmailResponse } from '@/types/auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

export function useVerification() {
  const isVerifying = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const result = ref<VerifyEmailResponse | null>(null)

  async function verifyEmail(verificationToken: string) {
    isVerifying.value = true
    error.value = null
    result.value = null

    try {
      const response = await fetch(`${API_BASE}/api/v1/auth/verify-email`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ verificationToken }),
      })
      const data = await response.json()

      if (response.ok) {
        result.value = data as VerifyEmailResponse
      } else {
        error.value = data as ProblemDetails
      }
    } finally {
      isVerifying.value = false
    }
  }

  return { isVerifying, error, result, verifyEmail }
}
