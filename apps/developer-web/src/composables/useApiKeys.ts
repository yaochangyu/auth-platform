import { ref } from 'vue'
import { apiRequest, problemOf } from '@/composables/useApi'
import type { ProblemDetails } from '@/types/application'
import type { ApiKeyListResponse, ApiKeyRequest, IssuedApiKey } from '@/types/apiKey'

export function useApiKeys(applicationId: string) {
  const base = `/api/v1/applications/${encodeURIComponent(applicationId)}/api-keys`
  const isLoading = ref(false)
  const isSaving = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const list = ref<ApiKeyListResponse>({ items: [], allowedScopes: [] })
  const issued = ref<IssuedApiKey | null>(null)

  async function run(path: string, init?: RequestInit): Promise<Response | null> {
    error.value = null
    const response = await apiRequest(path, init)
    if (response.ok) {
      return response
    }

    error.value = await problemOf(response)
    return null
  }

  async function fetchKeys() {
    isLoading.value = true
    try {
      list.value = (await (await run(base))?.json()) ?? list.value
    } finally {
      isLoading.value = false
    }
  }

  async function create(request: ApiKeyRequest): Promise<boolean> {
    isSaving.value = true
    try {
      const response = await run(base, { method: 'POST', body: JSON.stringify(request) })
      if (response) {
        issued.value = (await response.json()) as IssuedApiKey
        await fetchKeys()
      }

      return response !== null
    } finally {
      isSaving.value = false
    }
  }

  async function revoke(keyId: string) {
    isSaving.value = true
    try {
      if (await run(`${base}/${encodeURIComponent(keyId)}`, { method: 'DELETE' })) {
        await fetchKeys()
      }
    } finally {
      isSaving.value = false
    }
  }

  // 關閉後明文就無法再取得。
  function dismissIssued() {
    issued.value = null
  }

  return { isLoading, isSaving, error, list, issued, fetchKeys, create, revoke, dismissIssued }
}
