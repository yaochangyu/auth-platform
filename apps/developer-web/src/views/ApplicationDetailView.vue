<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import ApplicationForm from '@/components/ApplicationForm.vue'
import { useApplications } from '@/composables/useApplications'
import type { ApplicationRequest } from '@/types/application'

const applicationId = String(useRoute().params.applicationId)
const { isLoading, isSaving, error, application, fetchOne, update } = useApplications()
const saved = ref(false)

onMounted(() => fetchOne(applicationId))

async function onSubmit(request: ApplicationRequest) {
  saved.value = await update(applicationId, request)
}
</script>

<template>
  <main class="mx-auto max-w-xl space-y-6 px-4 py-8">
    <RouterLink to="/apps" class="text-sm text-muted-foreground hover:underline">← 返回應用程式列表</RouterLink>

    <p v-if="isLoading" class="text-sm text-muted-foreground">載入中...</p>
    <p v-else-if="error && !application" class="text-sm text-destructive">{{ error.title }}</p>

    <template v-else-if="application">
      <div class="space-y-1">
        <h1 class="text-2xl font-semibold text-foreground">{{ application.name }}</h1>
        <p class="text-sm text-muted-foreground">Client ID：<code>{{ application.clientId }}</code>（{{ application.status }}）</p>
      </div>

      <RouterLink :to="`/apps/${applicationId}/oauth`" class="inline-block text-sm font-medium text-foreground underline">
        OAuth 2.1 設定與 Client Secret →
      </RouterLink>

      <p v-if="error && !error.errors" class="text-sm text-destructive">{{ error.title }}</p>
      <p v-if="saved" class="text-sm text-muted-foreground">已儲存變更。</p>

      <ApplicationForm
        :initial="application"
        submit-label="儲存變更"
        :is-saving="isSaving"
        :errors="error?.errors"
        @submit="onSubmit"
      />
    </template>
  </main>
</template>

<style scoped></style>
