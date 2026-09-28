<script setup lang="ts">
import { reactive } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAuth } from '@/composables/useAuth'

const form = reactive({
  email: '',
  password: '',
  confirmPassword: '',
  displayName: '',
})

const { isSubmitting, error, result, register } = useAuth()

function onSubmit() {
  register({ ...form })
}
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-background px-4">
    <div class="w-full max-w-sm rounded-lg border bg-card p-8 shadow-sm">
      <h1 class="text-2xl font-semibold text-card-foreground">會員註冊</h1>

      <form v-if="!result" class="mt-6 space-y-4" @submit.prevent="onSubmit">
        <div class="space-y-2">
          <Label for="email">Email</Label>
          <Input id="email" v-model="form.email" type="email" required />
          <p v-if="error?.errors?.email" class="text-sm text-destructive">
            {{ error.errors.email[0] }}
          </p>
        </div>

        <div class="space-y-2">
          <Label for="displayName">暱稱</Label>
          <Input id="displayName" v-model="form.displayName" type="text" required />
          <p v-if="error?.errors?.displayName" class="text-sm text-destructive">
            {{ error.errors.displayName[0] }}
          </p>
        </div>

        <div class="space-y-2">
          <Label for="password">密碼</Label>
          <Input id="password" v-model="form.password" type="password" required />
          <p v-if="error?.errors?.password" class="text-sm text-destructive">
            {{ error.errors.password[0] }}
          </p>
        </div>

        <div class="space-y-2">
          <Label for="confirmPassword">確認密碼</Label>
          <Input id="confirmPassword" v-model="form.confirmPassword" type="password" required />
          <p v-if="error?.errors?.confirmPassword" class="text-sm text-destructive">
            {{ error.errors.confirmPassword[0] }}
          </p>
        </div>

        <p v-if="error && !error.errors" class="text-sm text-destructive">{{ error.title }}</p>

        <Button type="submit" class="w-full" :disabled="isSubmitting">
          {{ isSubmitting ? '送出中...' : '註冊' }}
        </Button>
      </form>

      <div v-else class="mt-6 space-y-2">
        <p class="text-card-foreground">{{ result.message }}</p>
        <p class="text-sm text-muted-foreground">會員狀態：{{ result.status }}</p>
      </div>
    </div>
  </main>
</template>

<style scoped></style>
