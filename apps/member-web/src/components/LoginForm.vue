<script setup lang="ts">
import { reactive, watch } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/composables/useAuth'
import { useCountdown } from '@/composables/useCountdown'

const form = reactive({
  email: '',
  password: '',
})

const { isSubmitting, error, loginAndRedirect } = useAuth()
const countdown = useCountdown()

watch(error, (value) => {
  if (value?.status === 423 && value.lockoutEndAt) {
    countdown.start(value.lockoutEndAt)
  } else {
    countdown.stop()
  }
})

watch(countdown.isRunning, (running) => {
  if (!running && error.value?.status === 423) {
    error.value = null
  }
})

function onSubmit() {
  loginAndRedirect({ email: form.email, password: form.password })
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

    <div v-if="error?.status === 423" class="text-sm text-destructive">
      <p>{{ error.title }}</p>
      <p v-if="countdown.isRunning">帳號已暫時鎖定，剩餘 {{ countdown.label }}</p>
    </div>
    <p v-else-if="error" class="text-sm text-destructive">{{ error.title }}</p>

    <Button type="submit" class="w-full" :disabled="isSubmitting || countdown.isRunning">
      {{ isSubmitting ? '登入中...' : '登入' }}
    </Button>

    <RouterLink to="/register" class="block text-center text-sm text-muted-foreground hover:underline">
      還沒有帳號？前往註冊
    </RouterLink>
  </form>
</template>

<style scoped></style>
