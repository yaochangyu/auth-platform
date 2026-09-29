<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import ClientSecretsPanel from '@/components/ClientSecretsPanel.vue'
import OAuthClientForm from '@/components/OAuthClientForm.vue'
import { useOAuthClient } from '@/composables/useOAuthClient'
import type { OAuthClientRequest } from '@/types/oauthClient'

const applicationId = String(useRoute().params.applicationId)
const { isLoading, isSaving, error, client, issuedSecret, fetchClient, save, issueSecret, revokeSecret, dismissSecret } =
  useOAuthClient(applicationId)
const saved = ref(false)

onMounted(fetchClient)

async function onSubmit(request: OAuthClientRequest) {
  saved.value = await save(request)
}
</script>

<template>
  <main class="mx-auto max-w-xl space-y-8 px-4 py-8">
    <RouterLink :to="`/apps/${applicationId}`" class="text-sm text-muted-foreground hover:underline">← 返回應用程式</RouterLink>
    <h1 class="text-2xl font-semibold text-foreground">OAuth 2.1 設定</h1>

    <p v-if="isLoading && !client" class="text-sm text-muted-foreground">載入中...</p>
    <p v-if="error && !error.errors" class="text-sm text-destructive">{{ error.title }}</p>

    <template v-if="client">
      <p class="text-sm text-muted-foreground">Client ID：<code>{{ client.clientId }}</code></p>
      <p v-if="saved" class="text-sm text-muted-foreground">已儲存 OAuth 設定。</p>

      <!-- 儲存後以最新內容重建表單（key），例如切換類型後 Secret 區塊會跟著出現或消失 -->
      <OAuthClientForm :key="JSON.stringify(client.redirectUris) + client.clientType" :client="client" :is-saving="isSaving" :errors="error?.errors" @submit="onSubmit" />

      <ClientSecretsPanel
        v-if="client.clientType === 'Confidential'"
        :secrets="client.secrets"
        :issued-secret="issuedSecret"
        :is-busy="isSaving"
        @issue="issueSecret"
        @revoke="revokeSecret"
        @dismiss="dismissSecret"
      />
      <p v-else class="text-sm text-muted-foreground">Public Client 不使用 Client Secret；改為 Confidential 並儲存後即可發行。</p>
    </template>
  </main>
</template>

<style scoped></style>
