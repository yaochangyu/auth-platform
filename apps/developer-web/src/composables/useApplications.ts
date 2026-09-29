import { ref } from 'vue'
import { justLoggedIn, useOAuth } from '@/composables/useOAuth'
import { useAuthStore } from '@/stores/auth'
import type { ApplicationDto, ApplicationListResponse, ApplicationRequest, ProblemDetails } from '@/types/application'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

export function useApplications() {
  const isLoading = ref(false)
  const isSaving = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const applications = ref<ApplicationDto[]>([])
  const application = ref<ApplicationDto | null>(null)

  async function request(path: string, init: RequestInit = {}): Promise<Response> {
    const auth = useAuthStore()
    const response = await fetch(`${API_BASE}${path}`, {
      ...init,
      headers: {
        ...(init.body ? { 'Content-Type': 'application/json' } : {}),
        Authorization: `Bearer ${auth.accessToken}`,
      },
    })

    // Access Token 過期或被撤銷：清除後走 SSO 無感重新登入，回到目前頁面。
    if (response.status === 401 && !justLoggedIn()) {
      auth.clear()
      await useOAuth().startLogin(window.location.pathname + window.location.search)
    }

    return response
  }

  async function load<T>(path: string, init?: RequestInit): Promise<T | null> {
    error.value = null
    const response = await request(path, init)
    if (response.ok) {
      return (await response.json()) as T
    }

    // 非 JSON 的錯誤回應（例如 Proxy 的 502）不能讓畫面崩潰。
    error.value = await response
      .json()
      .then((problem) => problem as ProblemDetails)
      .catch(() => ({ title: '發生未預期的錯誤，請稍後再試。', status: response.status }))
    return null
  }

  async function fetchList() {
    isLoading.value = true
    try {
      applications.value = (await load<ApplicationListResponse>('/api/v1/applications'))?.items ?? []
    } finally {
      isLoading.value = false
    }
  }

  async function fetchOne(applicationId: string) {
    isLoading.value = true
    try {
      application.value = await load<ApplicationDto>(`/api/v1/applications/${encodeURIComponent(applicationId)}`)
    } finally {
      isLoading.value = false
    }
  }

  async function create(body: ApplicationRequest): Promise<ApplicationDto | null> {
    isSaving.value = true
    try {
      return await load<ApplicationDto>('/api/v1/applications', { method: 'POST', body: JSON.stringify(body) })
    } finally {
      isSaving.value = false
    }
  }

  async function update(applicationId: string, body: ApplicationRequest): Promise<boolean> {
    isSaving.value = true
    try {
      const updated = await load<ApplicationDto>(`/api/v1/applications/${encodeURIComponent(applicationId)}`, {
        method: 'PUT',
        body: JSON.stringify(body),
      })
      if (updated) {
        application.value = updated
      }

      return updated !== null
    } finally {
      isSaving.value = false
    }
  }

  return { isLoading, isSaving, error, applications, application, fetchList, fetchOne, create, update }
}
