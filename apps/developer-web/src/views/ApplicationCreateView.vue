<script setup lang="ts">
import { useRouter } from 'vue-router'
import ApplicationForm from '@/components/ApplicationForm.vue'
import { useApplications } from '@/composables/useApplications'
import type { ApplicationRequest } from '@/types/application'

const router = useRouter()
const { isSaving, error, create } = useApplications()

async function onSubmit(request: ApplicationRequest) {
  const created = await create(request)
  if (created) {
    await router.push(`/apps/${created.id}`)
  }
}
</script>

<template>
  <main class="mx-auto max-w-xl space-y-6 px-4 py-8">
    <h1 class="text-2xl font-semibold text-foreground">建立新應用</h1>
    <p v-if="error && !error.errors" class="text-sm text-destructive">{{ error.title }}</p>
    <ApplicationForm submit-label="建立" :is-saving="isSaving" :errors="error?.errors" @submit="onSubmit" />
  </main>
</template>

<style scoped></style>
