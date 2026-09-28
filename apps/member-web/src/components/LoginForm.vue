<script setup lang="ts">
import { reactive } from 'vue'
import { useRoute } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/composables/useAuth'

const route = useRoute()

const form = reactive({
  email: '',
  password: '',
})

const { isSubmitting, error, loginAndRedirect } = useAuth()

function onSubmit() {
  const returnUrl = typeof route.query.returnUrl === 'string' ? route.query.returnUrl : undefined
  loginAndRedirect({ ...form, returnUrl })
}
</script>

<template>
  <form class="space-y-4" @submit.prevent="onSubmit">
    <div class="space-y-2">
      <Label for="email">Email</Label>
      <Input id="email" v-model="form.email" type="email" required />
    </div>

    <div class="space-y-2">
      <Label for="password">密碼</Label>
      <Input id="password" v-model="form.password" type="password" required />
    </div>

    <p v-if="error" class="text-sm text-destructive">{{ error.title }}</p>

    <Button type="submit" class="w-full" :disabled="isSubmitting">
      {{ isSubmitting ? '登入中...' : '登入' }}
    </Button>

    <RouterLink to="/register" class="block text-center text-sm text-muted-foreground hover:underline">
      還沒有帳號？前往註冊
    </RouterLink>
  </form>
</template>

<style scoped></style>
