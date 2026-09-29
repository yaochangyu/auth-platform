<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import ErrorNotice from '@/components/ErrorNotice.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuditLogs } from '@/composables/useAuditLogs'

const route = useRoute()
const { isLoading, error, logs, fetchLogs } = useAuditLogs()
const filters = reactive({ action: '', targetId: String(route.query.targetId ?? '') })
const page = ref(1)
const totalPages = computed(() => Math.max(1, Math.ceil(logs.value.total / logs.value.pageSize)))

onMounted(() => fetchLogs(filters, page.value))

function search() {
  page.value = 1
  fetchLogs(filters, page.value)
}

function go(target: number) {
  page.value = target
  fetchLogs(filters, page.value)
}
</script>

<template>
  <main class="mx-auto max-w-4xl space-y-6 px-4 py-8">
    <h1 class="text-2xl font-semibold text-foreground">稽核紀錄</h1>
    <p class="text-sm text-muted-foreground">只能查詢，無法修改或刪除；資料庫也禁止更新、刪除與清空。</p>

    <form class="flex flex-wrap items-end gap-3" @submit.prevent="search">
      <div class="space-y-1">
        <Label for="action">動作</Label>
        <select id="action" v-model="filters.action" class="h-8 rounded-lg border bg-transparent px-2 text-sm">
          <option value="">全部</option>
          <option value="application.suspend">application.suspend</option>
          <option value="application.activate">application.activate</option>
        </select>
      </div>
      <div class="min-w-64 flex-1 space-y-1">
        <Label for="targetId">目標 ID</Label>
        <Input id="targetId" v-model="filters.targetId" placeholder="應用程式 ID" />
      </div>
      <Button type="submit" :disabled="isLoading">查詢</Button>
    </form>

    <ErrorNotice :error="error" />
    <p v-if="isLoading" class="text-sm text-muted-foreground">載入中...</p>
    <p v-else-if="!error && logs.items.length === 0" class="text-sm text-muted-foreground">沒有符合條件的稽核紀錄。</p>

    <ul v-else class="space-y-3">
      <li v-for="log in logs.items" :key="log.id" class="space-y-2 rounded-lg border bg-card p-4">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <p class="font-mono text-sm font-medium text-card-foreground">{{ log.action }}</p>
          <p class="text-xs text-muted-foreground">{{ new Date(log.occurredAt).toLocaleString() }}・{{ log.clientIp }}</p>
        </div>
        <p class="text-xs text-muted-foreground">操作人：<code>{{ log.actorMemberId }}</code>　目標：{{ log.targetType }} <code>{{ log.targetId }}</code></p>
        <div class="grid gap-2 text-xs sm:grid-cols-3">
          <div><p class="text-muted-foreground">變更前</p><pre class="overflow-x-auto rounded bg-muted p-2">{{ JSON.stringify(log.before, null, 1) }}</pre></div>
          <div><p class="text-muted-foreground">變更後</p><pre class="overflow-x-auto rounded bg-muted p-2">{{ JSON.stringify(log.after, null, 1) }}</pre></div>
          <div><p class="text-muted-foreground">補充</p><pre class="overflow-x-auto rounded bg-muted p-2">{{ JSON.stringify(log.details, null, 1) }}</pre></div>
        </div>
      </li>
    </ul>

    <div v-if="logs.total > logs.pageSize" class="flex items-center justify-between text-sm text-muted-foreground">
      <span>共 {{ logs.total }} 筆，第 {{ logs.page }} / {{ totalPages }} 頁</span>
      <div class="flex gap-2">
        <Button size="sm" variant="outline" :disabled="page <= 1" @click="go(page - 1)">上一頁</Button>
        <Button size="sm" variant="outline" :disabled="page >= totalPages" @click="go(page + 1)">下一頁</Button>
      </div>
    </div>
  </main>
</template>

<style scoped></style>
