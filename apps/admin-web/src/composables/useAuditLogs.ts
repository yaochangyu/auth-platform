import { ref } from 'vue'
import { apiRequest, problemOf } from '@/composables/useApi'
import type { AuditLogList } from '@/types/admin'
import type { ProblemDetails } from '@/types/problem'

export function useAuditLogs() {
  const isLoading = ref(false)
  const error = ref<ProblemDetails | null>(null)
  const logs = ref<AuditLogList>({ items: [], total: 0, page: 1, pageSize: 50 })

  async function fetchLogs(filters: { action: string; targetId: string }, page: number) {
    isLoading.value = true
    error.value = null
    try {
      const query = new URLSearchParams({ page: String(page), pageSize: '50' })
      if (filters.action) {
        query.set('action', filters.action)
      }

      if (filters.targetId) {
        query.set('targetId', filters.targetId)
      }

      const response = await apiRequest(`/api/v1/admin/audit-logs?${query}`)
      if (response.ok) {
        logs.value = (await response.json()) as AuditLogList
      } else {
        error.value = await problemOf(response)
      }
    } finally {
      isLoading.value = false
    }
  }

  return { isLoading, error, logs, fetchLogs }
}
