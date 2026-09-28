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

const { isSubmitting, error, loginResult, login, logout } = useAuth()

async function onSubmit() {
  const returnUrl = typeof route.query.returnUrl === 'string' ? route.query.returnUrl : undefined
  const ok = await login({ ...form, returnUrl })
  if (ok && returnUrl) {
    window.location.href = returnUrl
  }
}

async function onLogout() {
  await logout()
  loginResult.value = null
  form.email = ''
  form.password = ''
}
</script>

<template>
  <form v-if="!loginResult" class="space-y-4" @submit.prevent="onSubmit">
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

  <div v-else class="space-y-4">
    <p class="text-card-foreground">歡迎回來，{{ loginResult.displayName }}</p>
    <Button variant="outline" class="w-full" @click="onLogout">登出</Button>
  </div>
</template>

<style scoped></style>
