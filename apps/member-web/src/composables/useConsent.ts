import { ref } from 'vue'
import type { ConsentDetailsResponse, ProblemDetails } from '@/types/auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

// openid 是識別身分的必要範疇，會員只能調整其餘範疇。
const REQUIRED_SCOPES = ['openid']

export function useConsent(consentId: string) {
  const isLoading = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const details = ref<ConsentDetailsResponse | null>(null)
  const selectedScopes = ref<string[]>([])

  // 同意/拒絕以原生表單 POST 提交，由 Auth Server 直接 302 導回第三方應用程式（RFC 6749）。
  const submitUrl = `${API_BASE}/api/v1/oauth/consent/${encodeURIComponent(consentId)}`

  async function fetchDetails() {
    if (!consentId) {
      error.value = { type: 'about:blank', title: '缺少授權請求資訊，請由應用程式重新發起授權。', status: 400 }
      return
    }

    isLoading.value = true
    error.value = null

    try {
      const response = await fetch(submitUrl, { credentials: 'include' })

      if (response.ok) {
        details.value = (await response.json()) as ConsentDetailsResponse
        selectedScopes.value = details.value.scopes.map((scope) => scope.name)
      } else {
        error.value = (await response.json()) as ProblemDetails
      }
    } finally {
      isLoading.value = false
    }
  }

  function isRequired(scopeName: string) {
    return REQUIRED_SCOPES.includes(scopeName)
  }

  return { isLoading, error, details, selectedScopes, submitUrl, fetchDetails, isRequired }
}
