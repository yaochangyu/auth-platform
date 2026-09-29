<script setup lang="ts">
import type { ApplicationStatus } from '@/types/admin'

defineProps<{ status: ApplicationStatus }>()

const labels: Record<ApplicationStatus, string> = {
  Active: '啟用中',
  PendingReview: '待審核',
  Suspended: '已停用',
}
</script>

<template>
  <span
    class="shrink-0 rounded-md border px-2 py-0.5 text-xs"
    :class="status === 'Suspended' ? 'border-destructive/50 text-destructive' : 'text-muted-foreground'"
  >
    {{ labels[status] }}
  </span>
</template>
