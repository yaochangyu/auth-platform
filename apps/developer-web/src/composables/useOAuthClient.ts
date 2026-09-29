import { ref } from 'vue'
import { apiRequest, problemOf } from '@/composables/useApi'
import type { ProblemDetails } from '@/types/application'
import type { IssuedClientSecret, OAuthClientDto, OAuthClientRequest } from '@/types/oauthClient'

export function useOAuthClient(applicationId: string) {
  const base = `/api/v1/applications/${encodeURIComponent(applicationId)}/oauth-client`
  const isLoading = ref(false)
  const isSaving = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const client = ref<OAuthClientDto | null>(null)
  const issuedSecret = ref<IssuedClientSecret | null>(null)

  async function run(path: string, init?: RequestInit): Promise<Response | null> {
    error.value = null
    const response = await apiRequest(path, init)
    if (response.ok) {
      return response
    }

    error.value = await problemOf(response)
    return null
  }

  async function fetchClient() {
    isLoading.value = true
    try {
      client.value = (await (await run(base))?.json()) ?? client.value
    } finally {
      isLoading.value = false
    }
  }

  async function save(request: OAuthClientRequest): Promise<boolean> {
    isSaving.value = true
    try {
      const response = await run(base, { method: 'PUT', body: JSON.stringify(request) })
      if (response) {
        client.value = (await response.json()) as OAuthClientDto
      }

      return response !== null
    } finally {
      isSaving.value = false
    }
  }

  async function issueSecret() {
    isSaving.value = true
    try {
      const response = await run(`${base}/secrets`, { method: 'POST' })
      if (response) {
        issuedSecret.value = (await response.json()) as IssuedClientSecret
        await fetchClient()
      }
    } finally {
      isSaving.value = false
    }
  }

  async function revokeSecret(secretId: string) {
    isSaving.value = true
    try {
      if (await run(`${base}/secrets/${encodeURIComponent(secretId)}`, { method: 'DELETE' })) {
        await fetchClient()
      }
    } finally {
      isSaving.value = false
    }
  }

  // 關閉後明文就無法再取得。
  function dismissSecret() {
    issuedSecret.value = null
  }

  return { isLoading, isSaving, error, client, issuedSecret, fetchClient, save, issueSecret, revokeSecret, dismissSecret }
}
