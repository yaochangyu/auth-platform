import { ref } from 'vue'
import { apiRequest, problemOf } from '@/composables/useApi'
import type { ApplicationDto, ApplicationListResponse, ApplicationRequest, ProblemDetails } from '@/types/application'

export function useApplications() {
  const isLoading = ref(false)
  const isSaving = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const applications = ref<ApplicationDto[]>([])
  const application = ref<ApplicationDto | null>(null)

  async function load<T>(path: string, init?: RequestInit): Promise<T | null> {
    error.value = null
    const response = await apiRequest(path, init)
    if (response.ok) {
      return (await response.json()) as T
    }

    error.value = await problemOf(response)
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
