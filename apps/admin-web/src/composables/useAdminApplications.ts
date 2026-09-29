import { ref } from 'vue'
import { apiRequest, problemOf } from '@/composables/useApi'
import type { AdminApplication, AdminApplicationList, ApplicationStatus, StatusChangeResponse } from '@/types/admin'
import type { ProblemDetails } from '@/types/problem'

export function useAdminApplications() {
  const isLoading = ref(false)
  const isSaving = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const list = ref<AdminApplicationList>({ items: [], total: 0, page: 1, pageSize: 20 })
  const application = ref<AdminApplication | null>(null)
  const lastChange = ref<StatusChangeResponse | null>(null)

  async function run(path: string, init?: RequestInit): Promise<Response | null> {
    error.value = null
    const response = await apiRequest(path, init)
    if (response.ok) {
      return response
    }

    error.value = await problemOf(response)
    return null
  }

  async function fetchList(status: ApplicationStatus | '', page: number) {
    isLoading.value = true
    try {
      const query = new URLSearchParams({ page: String(page), pageSize: '20' })
      if (status) {
        query.set('status', status)
      }

      list.value = (await (await run(`/api/v1/admin/applications?${query}`))?.json()) ?? list.value
    } finally {
      isLoading.value = false
    }
  }

  async function fetchOne(applicationId: string) {
    isLoading.value = true
    try {
      application.value = (await (await run(`/api/v1/admin/applications/${encodeURIComponent(applicationId)}`))?.json()) ?? null
    } finally {
      isLoading.value = false
    }
  }

  async function changeStatus(applicationId: string, status: ApplicationStatus, reason: string): Promise<boolean> {
    isSaving.value = true
    try {
      const response = await run(`/api/v1/admin/applications/${encodeURIComponent(applicationId)}/status`, {
        method: 'PUT',
        body: JSON.stringify({ status, reason: reason || null }),
      })
      if (response) {
        lastChange.value = (await response.json()) as StatusChangeResponse
        application.value = lastChange.value.application
      }

      return response !== null
    } finally {
      isSaving.value = false
    }
  }

  return { isLoading, isSaving, error, list, application, lastChange, fetchList, fetchOne, changeStatus }
}
