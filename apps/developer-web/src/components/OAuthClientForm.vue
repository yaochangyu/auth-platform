<script setup lang="ts">
import { reactive } from 'vue'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { OAuthClientDto, OAuthClientRequest, OAuthClientType } from '@/types/oauthClient'

const props = defineProps<{
  client: OAuthClientDto
  isSaving: boolean
  errors?: Record<string, string[]>
}>()

const emit = defineEmits<{
  submit: [value: OAuthClientRequest]
}>()

const lines = (values: string[]) => values.join('\n')
const parseLines = (text: string) => text.split('\n').map((line) => line.trim()).filter(Boolean)

const form = reactive({
  clientType: props.client.clientType as OAuthClientType,
  redirectUris: lines(props.client.redirectUris),
  postLogoutRedirectUris: lines(props.client.postLogoutRedirectUris),
  scopes: [...props.client.scopes],
})

// 集合欄位的錯誤鍵帶有索引（例如 redirectUris[0]），一併顯示。
const fieldErrors = (field: string) =>
  Object.entries(props.errors ?? {})
    .filter(([key]) => key === field || key.startsWith(`${field}[`))
    .flatMap(([, messages]) => messages)

function onSubmit() {
  emit('submit', {
    clientType: form.clientType,
    redirectUris: parseLines(form.redirectUris),
    postLogoutRedirectUris: parseLines(form.postLogoutRedirectUris),
    scopes: form.scopes,
  })
}
</script>

<template>
  <form class="space-y-6" @submit.prevent="onSubmit">
    <fieldset class="space-y-2">
      <legend class="text-sm font-medium">客戶端類型</legend>
      <div class="flex items-start gap-3">
        <input id="type-public" v-model="form.clientType" type="radio" value="Public" class="mt-1 size-4 accent-primary" />
        <Label for="type-public">Public：純前端 SPA 或行動 App，強制 PKCE，不使用 Client Secret</Label>
      </div>
      <div class="flex items-start gap-3">
        <input id="type-confidential" v-model="form.clientType" type="radio" value="Confidential" class="mt-1 size-4 accent-primary" />
        <Label for="type-confidential">Confidential：具備安全後端的應用，使用 Client Secret 換發 Token</Label>
      </div>
    </fieldset>

    <div class="space-y-2">
      <Label for="redirectUris">Redirect URIs（每行一個，嚴格比對）</Label>
      <p v-if="form.clientType === 'Public'" class="text-xs text-muted-foreground">
        原生行動 App（Public Client）強制要求使用 HTTPS 官方網域深層連結（iOS Universal Links / Android App Links），杜絕惡意 URL Scheme 劫持。
      </p>
      <Textarea id="redirectUris" v-model="form.redirectUris" placeholder="https://app.example.com/callback" />
      <p v-for="message in fieldErrors('redirectUris')" :key="message" class="text-sm text-destructive">{{ message }}</p>
    </div>

    <div class="space-y-2">
      <Label for="postLogoutRedirectUris">Post Logout Redirect URIs（每行一個）</Label>
      <Textarea id="postLogoutRedirectUris" v-model="form.postLogoutRedirectUris" />
      <p v-for="message in fieldErrors('postLogoutRedirectUris')" :key="message" class="text-sm text-destructive">{{ message }}</p>
    </div>

    <fieldset class="space-y-2">
      <legend class="text-sm font-medium">申請的存取範疇</legend>
      <div v-for="scope in client.allowedScopes" :key="scope" class="flex items-center gap-3">
        <input :id="`scope-${scope}`" v-model="form.scopes" type="checkbox" :value="scope" class="size-4 accent-primary" />
        <Label :for="`scope-${scope}`">{{ scope }}</Label>
      </div>
      <p v-for="message in fieldErrors('scopes')" :key="message" class="text-sm text-destructive">{{ message }}</p>
    </fieldset>

    <Button type="submit" class="w-full" :disabled="isSaving">{{ isSaving ? '儲存中...' : '儲存 OAuth 設定' }}</Button>
  </form>
</template>

<style scoped></style>
