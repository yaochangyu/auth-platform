<script setup lang="ts">
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import type { ApiKeySummary } from '@/types/apiKey'

defineProps<{
  keys: ApiKeySummary[]
  isBusy: boolean
}>()

const emit = defineEmits<{
  revoke: [keyId: string]
}>()

const statusLabel: Record<ApiKeySummary['status'], string> = {
  Active: '使用中',
  Expired: '已過期',
  Revoked: '已撤銷',
}
</script>

<template>
  <p v-if="keys.length === 0" class="text-sm text-muted-foreground">尚未建立任何 API Key。</p>
  <ul v-else class="space-y-2">
    <li v-for="key in keys" :key="key.id" class="flex items-center justify-between rounded-md border p-3">
      <div class="min-w-0">
        <p class="truncate font-medium text-card-foreground">{{ key.name }}</p>
        <p class="font-mono text-xs text-muted-foreground">{{ key.prefix }}…</p>
        <p class="text-xs text-muted-foreground">
          {{ statusLabel[key.status] }}・範疇：{{ key.scopes.join('、') }}
          <template v-if="key.expiresAt">・到期：{{ new Date(key.expiresAt).toLocaleDateString() }}</template>
        </p>
      </div>

      <AlertDialog v-if="key.status === 'Active'">
        <AlertDialogTrigger as-child>
          <Button variant="outline" size="sm" :disabled="isBusy">撤銷</Button>
        </AlertDialogTrigger>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>確定要撤銷「{{ key.name }}」？</AlertDialogTitle>
            <AlertDialogDescription>撤銷後立即失效，仍使用這把 API Key 的服務將被拒絕，此操作無法復原。</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction @click="emit('revoke', key.id)">確定撤銷</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </li>
  </ul>
</template>

<style scoped></style>
