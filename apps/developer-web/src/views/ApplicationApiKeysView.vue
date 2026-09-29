<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'
import ApiKeyForm from '@/components/ApiKeyForm.vue'
import ApiKeyList from '@/components/ApiKeyList.vue'
import IssuedApiKeyAlert from '@/components/IssuedApiKeyAlert.vue'
import { useApiKeys } from '@/composables/useApiKeys'

const applicationId = String(useRoute().params.applicationId)
const { isLoading, isSaving, error, list, issued, fetchKeys, create, revoke, dismissIssued } = useApiKeys(applicationId)

onMounted(fetchKeys)
</script>

<template>
  <main class="mx-auto max-w-xl space-y-6 px-4 py-8">
    <RouterLink :to="`/apps/${applicationId}`" class="text-sm text-muted-foreground hover:underline">← 返回應用程式</RouterLink>
    <h1 class="text-2xl font-semibold text-foreground">機器存取憑據（API Key）</h1>
    <p class="text-sm text-muted-foreground">
      供沒有固定 IP 的後端服務呼叫平台。請求需帶 X-Api-Key、X-Timestamp（±5 分鐘內）與 X-Signature（以 API Secret 對
      Method、Path、Timestamp、Body 計算的 HMAC-SHA256）。
    </p>

    <p v-if="error && !error.errors" class="text-sm text-destructive">{{ error.title }}</p>

    <IssuedApiKeyAlert v-if="issued" :issued="issued" @dismiss="dismissIssued" />

    <p v-if="isLoading" class="text-sm text-muted-foreground">載入中...</p>
    <ApiKeyList v-else :keys="list.items" :is-busy="isSaving" @revoke="revoke" />

    <ApiKeyForm :allowed-scopes="list.allowedScopes" :is-saving="isSaving" :errors="error?.errors" @submit="create" />
  </main>
</template>

<style scoped></style>
