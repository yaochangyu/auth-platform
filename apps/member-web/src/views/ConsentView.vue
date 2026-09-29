<script setup lang="ts">
import { useRoute } from 'vue-router'
import ConsentForm from '@/components/ConsentForm.vue'

const consentId = String(useRoute().query.consent_id ?? '')
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-background px-4">
    <div class="w-full max-w-md rounded-lg border bg-card p-8 shadow-sm">
      <h1 class="text-2xl font-semibold text-card-foreground">授權確認</h1>
      <div class="mt-6">
        <ConsentForm :consent-id="consentId" />
      </div>
    </div>
  </main>
</template>

<style scoped></style>
