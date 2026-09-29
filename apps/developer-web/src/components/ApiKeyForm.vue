<script setup lang="ts">
import { reactive } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { ApiKeyEnvironment, ApiKeyRequest } from '@/types/apiKey'

const props = defineProps<{
  allowedScopes: string[]
  isSaving: boolean
  errors?: Record<string, string[]>
}>()

const emit = defineEmits<{
  submit: [value: ApiKeyRequest]
}>()

const form = reactive({
  name: '',
  environment: 'Live' as ApiKeyEnvironment,
  scopes: [] as string[],
  expiresOn: '',
})

const fieldErrors = (field: string) =>
  Object.entries(props.errors ?? {})
    .filter(([key]) => key === field || key.startsWith(`${field}[`))
    .flatMap(([, messages]) => messages)

function onSubmit() {
  emit('submit', {
    name: form.name,
    environment: form.environment,
    scopes: form.scopes,
    // 到期日當天結束（本地時間）才失效。
    expiresAt: form.expiresOn ? new Date(`${form.expiresOn}T23:59:59`).toISOString() : null,
  })
}
</script>

<template>
  <form class="space-y-4 rounded-lg border bg-card p-4" @submit.prevent="onSubmit">
    <h2 class="text-lg font-semibold text-card-foreground">建立 API Key</h2>

    <div class="space-y-2">
      <Label for="keyName">名稱（用途說明）</Label>
      <Input id="keyName" v-model="form.name" maxlength="100" required />
      <p v-for="message in fieldErrors('name')" :key="message" class="text-sm text-destructive">{{ message }}</p>
    </div>

    <fieldset class="space-y-2">
      <legend class="text-sm font-medium">環境</legend>
      <div class="flex items-center gap-3">
        <input id="env-live" v-model="form.environment" type="radio" value="Live" class="size-4 accent-primary" />
        <Label for="env-live">正式機（ak_live_…）</Label>
      </div>
      <div class="flex items-center gap-3">
        <input id="env-test" v-model="form.environment" type="radio" value="Test" class="size-4 accent-primary" />
        <Label for="env-test">測試機（ak_test_…）</Label>
      </div>
    </fieldset>

    <fieldset class="space-y-2">
      <legend class="text-sm font-medium">權限範疇</legend>
      <div v-for="scope in allowedScopes" :key="scope" class="flex items-center gap-3">
        <input :id="`key-scope-${scope}`" v-model="form.scopes" type="checkbox" :value="scope" class="size-4 accent-primary" />
        <Label :for="`key-scope-${scope}`">{{ scope }}</Label>
      </div>
      <p v-for="message in fieldErrors('scopes')" :key="message" class="text-sm text-destructive">{{ message }}</p>
    </fieldset>

    <div class="space-y-2">
      <Label for="expiresOn">到期日（選填，不填為不過期）</Label>
      <Input id="expiresOn" v-model="form.expiresOn" type="date" />
      <p v-for="message in fieldErrors('expiresAt')" :key="message" class="text-sm text-destructive">{{ message }}</p>
    </div>

    <Button type="submit" class="w-full" :disabled="isSaving">{{ isSaving ? '建立中...' : '建立 API Key' }}</Button>
  </form>
</template>

<style scoped></style>
