<script setup lang="ts">
import { reactive } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { ApplicationRequest } from '@/types/application'

const props = defineProps<{
  initial?: ApplicationRequest
  submitLabel: string
  isSaving: boolean
  errors?: Record<string, string[]>
}>()

const emit = defineEmits<{
  submit: [value: ApplicationRequest]
}>()

const form = reactive({
  name: props.initial?.name ?? '',
  description: props.initial?.description ?? '',
  contactEmail: props.initial?.contactEmail ?? '',
  logoUrl: props.initial?.logoUrl ?? '',
  homepageUrl: props.initial?.homepageUrl ?? '',
})

function onSubmit() {
  emit('submit', {
    name: form.name,
    description: form.description,
    contactEmail: form.contactEmail,
    logoUrl: form.logoUrl || null,
    homepageUrl: form.homepageUrl || null,
  })
}
</script>

<template>
  <form class="space-y-4" @submit.prevent="onSubmit">
    <div class="space-y-2">
      <Label for="name">應用程式名稱</Label>
      <Input id="name" v-model="form.name" maxlength="100" required />
      <p v-if="errors?.name" class="text-sm text-destructive">{{ errors.name[0] }}</p>
    </div>

    <div class="space-y-2">
      <Label for="description">簡介</Label>
      <Textarea id="description" v-model="form.description" maxlength="500" required />
      <p v-if="errors?.description" class="text-sm text-destructive">{{ errors.description[0] }}</p>
    </div>

    <div class="space-y-2">
      <Label for="contactEmail">聯絡窗口 Email</Label>
      <Input id="contactEmail" v-model="form.contactEmail" type="email" required />
      <p v-if="errors?.contactEmail" class="text-sm text-destructive">{{ errors.contactEmail[0] }}</p>
    </div>

    <div class="space-y-2">
      <Label for="logoUrl">Logo 網址（選填）</Label>
      <Input id="logoUrl" v-model="form.logoUrl" type="url" placeholder="https://" />
      <p v-if="errors?.logoUrl" class="text-sm text-destructive">{{ errors.logoUrl[0] }}</p>
    </div>

    <div class="space-y-2">
      <Label for="homepageUrl">官網首頁（選填）</Label>
      <Input id="homepageUrl" v-model="form.homepageUrl" type="url" placeholder="https://" />
      <p v-if="errors?.homepageUrl" class="text-sm text-destructive">{{ errors.homepageUrl[0] }}</p>
    </div>

    <Button type="submit" class="w-full" :disabled="isSaving">{{ isSaving ? '儲存中...' : submitLabel }}</Button>
  </form>
</template>

<style scoped></style>
