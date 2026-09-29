<script setup lang="ts">
import { onMounted } from 'vue'
import { Button } from '@/components/ui/button'
import { useApplications } from '@/composables/useApplications'

const { isLoading, error, applications, fetchList } = useApplications()

onMounted(fetchList)
</script>

<template>
  <main class="mx-auto max-w-3xl space-y-6 px-4 py-8">
    <div class="flex items-center justify-between">
      <h1 class="text-2xl font-semibold text-foreground">我的應用程式</h1>
      <Button as-child><RouterLink to="/apps/new">建立新應用</RouterLink></Button>
    </div>

    <p v-if="error" class="text-sm text-destructive">{{ error.title }}</p>
    <p v-if="isLoading" class="text-sm text-muted-foreground">載入中...</p>
    <p v-else-if="applications.length === 0" class="text-sm text-muted-foreground">
      目前沒有應用程式，點選「建立新應用」開始串接。
    </p>

    <ul v-else class="space-y-3">
      <li v-for="app in applications" :key="app.id">
        <RouterLink
          :to="`/apps/${app.id}`"
          class="flex items-center justify-between rounded-lg border bg-card p-4 shadow-sm hover:bg-accent"
        >
          <div class="min-w-0">
            <p class="truncate font-medium text-card-foreground">{{ app.name }}</p>
            <p class="truncate text-sm text-muted-foreground">{{ app.description }}</p>
          </div>
          <span class="ml-4 shrink-0 rounded-md border px-2 py-0.5 text-xs text-muted-foreground">{{ app.status }}</span>
        </RouterLink>
      </li>
    </ul>
  </main>
</template>

<style scoped></style>
