<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import ErrorNotice from '@/components/ErrorNotice.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { useAdminApplications } from '@/composables/useAdminApplications'
import type { ApplicationStatus } from '@/types/admin'

const { isLoading, error, list, fetchList } = useAdminApplications()
const status = ref<ApplicationStatus | ''>('')
const page = ref(1)
const totalPages = computed(() => Math.max(1, Math.ceil(list.value.total / list.value.pageSize)))

onMounted(() => fetchList(status.value, page.value))

watch(status, () => {
  page.value = 1
  fetchList(status.value, page.value)
})

function go(target: number) {
  page.value = target
  fetchList(status.value, page.value)
}
</script>

<template>
  <main class="mx-auto max-w-4xl space-y-6 px-4 py-8">
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-semibold text-foreground">全平台應用程式</h1>
      <div class="flex items-center gap-2">
        <Label for="statusFilter">狀態</Label>
        <select id="statusFilter" v-model="status" class="h-8 rounded-lg border bg-transparent px-2 text-sm">
          <option value="">全部</option>
          <option value="PendingReview">待審核</option>
          <option value="Active">啟用中</option>
          <option value="Suspended">已停用</option>
        </select>
      </div>
    </div>

    <ErrorNotice :error="error" />
    <p v-if="isLoading" class="text-sm text-muted-foreground">載入中...</p>
    <p v-else-if="!error && list.items.length === 0" class="text-sm text-muted-foreground">沒有符合條件的應用程式。</p>

    <ul v-else class="space-y-2">
      <li v-for="app in list.items" :key="app.id">
        <RouterLink :to="`/applications/${app.id}`" class="flex items-center justify-between rounded-lg border bg-card p-4 shadow-sm hover:bg-accent">
          <div class="min-w-0">
            <div class="flex items-center gap-2">
              <p class="truncate font-medium text-card-foreground">{{ app.name }}</p>
              <span v-if="app.clientType" class="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
                {{ app.clientType }}
              </span>
            </div>
            <p class="truncate text-xs text-muted-foreground">擁有者：{{ app.ownerMemberId }}・{{ app.contactEmail }}</p>
          </div>
          <StatusBadge :status="app.status" />
        </RouterLink>
      </li>
    </ul>

    <div v-if="list.total > list.pageSize" class="flex items-center justify-between text-sm text-muted-foreground">
      <span>共 {{ list.total }} 筆，第 {{ list.page }} / {{ totalPages }} 頁</span>
      <div class="flex gap-2">
        <Button size="sm" variant="outline" :disabled="page <= 1" @click="go(page - 1)">上一頁</Button>
        <Button size="sm" variant="outline" :disabled="page >= totalPages" @click="go(page + 1)">下一頁</Button>
      </div>
    </div>
  </main>
</template>

<style scoped></style>
