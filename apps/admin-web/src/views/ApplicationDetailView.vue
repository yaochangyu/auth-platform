<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import ErrorNotice from '@/components/ErrorNotice.vue'
import StatusBadge from '@/components/StatusBadge.vue'
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
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { useAdminApplications } from '@/composables/useAdminApplications'

const applicationId = String(useRoute().params.applicationId)
const { isLoading, isSaving, error, application, lastChange, fetchOne, changeStatus } = useAdminApplications()
const reason = ref('')

onMounted(() => fetchOne(applicationId))

async function suspend() {
  if (await changeStatus(applicationId, 'Suspended', reason.value)) {
    reason.value = ''
  }
}

async function activate() {
  if (await changeStatus(applicationId, 'Active', reason.value)) {
    reason.value = ''
  }
}
</script>

<template>
  <main class="mx-auto max-w-2xl space-y-6 px-4 py-8">
    <RouterLink to="/applications" class="text-sm text-muted-foreground hover:underline">← 返回應用程式列表</RouterLink>

    <p v-if="isLoading" class="text-sm text-muted-foreground">載入中...</p>
    <ErrorNotice :error="error" />

    <template v-if="application">
      <div class="space-y-2">
        <div class="flex items-center gap-3">
          <h1 class="text-2xl font-semibold text-foreground">{{ application.name }}</h1>
          <StatusBadge :status="application.status" />
        </div>
        <p class="text-sm text-muted-foreground">{{ application.description }}</p>
        <dl class="grid grid-cols-[8rem_1fr] gap-y-1 text-sm">
          <dt class="text-muted-foreground">Client ID</dt>
          <dd><code>{{ application.clientId }}</code></dd>
          <dt class="text-muted-foreground">擁有者</dt>
          <dd><code>{{ application.ownerMemberId }}</code></dd>
          <dt class="text-muted-foreground">聯絡窗口</dt>
          <dd>{{ application.contactEmail }}</dd>
        </dl>
      </div>

      <section class="space-y-3 rounded-lg border p-4">
        <h2 class="text-lg font-semibold text-foreground">審核與緊急斷路</h2>
        <div class="space-y-2">
          <Label for="reason">原因（停用時必填，會寫入稽核紀錄）</Label>
          <Textarea id="reason" v-model="reason" maxlength="500" />
          <p v-if="error?.errors?.reason" class="text-sm text-destructive">{{ error.errors.reason[0] }}</p>
        </div>

        <AlertDialog v-if="application.status !== 'Suspended'">
          <AlertDialogTrigger as-child>
            <Button variant="outline" :disabled="isSaving">停用應用程式（緊急斷路）</Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>確定要停用「{{ application.name }}」？</AlertDialogTitle>
              <AlertDialogDescription>
                停用後會立即作廢該應用程式所有的授權、Token 與 API Key，且此操作無法復原：之後取消停用，使用者需要重新授權，
                開發者需要重新發行 API Key。此動作會記錄在稽核紀錄中。
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>取消</AlertDialogCancel>
              <AlertDialogAction @click="suspend">確定停用</AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        <Button v-if="application.status !== 'Active'" :disabled="isSaving" @click="activate">
          {{ application.status === 'PendingReview' ? '核准上線' : '取消停用' }}
        </Button>

        <p v-if="lastChange?.circuitBreaker" class="text-sm text-muted-foreground" role="status">
          已作廢 {{ lastChange.circuitBreaker.revokedApiKeys }} 把 API Key、
          {{ lastChange.circuitBreaker.revokedAuthorizations }} 筆授權、{{ lastChange.circuitBreaker.revokedTokens }} 個 Token。
        </p>
      </section>

      <RouterLink :to="`/audit-logs?targetId=${application.id}`" class="inline-block text-sm font-medium text-foreground underline">
        查看這個應用程式的稽核紀錄 →
      </RouterLink>
    </template>
  </main>
</template>

<style scoped></style>
