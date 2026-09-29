import { ref } from 'vue'
import type { ConnectedAppDto, ConnectedAppListResponse, ProblemDetails } from '@/types/auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

export function useConnectedApps() {
  const isLoading = ref(false)
  const isRevoking = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const apps = ref<ConnectedAppDto[]>([])

  async function fetchApps() {
    isLoading.value = true
    error.value = null

    try {
      const response = await fetch(`${API_BASE}/api/v1/member/connected-apps`, {
        credentials: 'include',
      })

      if (response.ok) {
        const data = (await response.json()) as ConnectedAppListResponse
        apps.value = data.items
      } else {
        error.value = (await response.json()) as ProblemDetails
      }
    } finally {
      isLoading.value = false
    }
  }

  async function revokeApp(appId: string) {
    isRevoking.value = true
    error.value = null

    try {
      const response = await fetch(`${API_BASE}/api/v1/member/connected-apps/${appId}`, {
        method: 'DELETE',
        credentials: 'include',
      })

      if (response.ok) {
        apps.value = apps.value.filter((app) => app.appId !== appId)
      } else {
        error.value = (await response.json()) as ProblemDetails
      }

      return response.ok
    } finally {
      isRevoking.value = false
    }
  }

  return { isLoading, isRevoking, error, apps, fetchApps, revokeApp }
}
