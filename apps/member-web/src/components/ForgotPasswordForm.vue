<script setup lang="ts">
import { reactive } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { usePasswordReset } from '@/composables/usePasswordReset'

const form = reactive({
  email: '',
})

const { isSubmitting, error, forgotPasswordResult, forgotPassword } = usePasswordReset()

function onSubmit() {
  forgotPassword({ ...form })
}
</script>

<template>
  <form v-if="!forgotPasswordResult" class="space-y-4" @submit.prevent="onSubmit">
    <div class="space-y-2">
      <Label for="email">Email</Label>
      <Input id="email" v-model="form.email" type="email" required />
    </div>

    <p v-if="error" class="text-sm text-destructive">{{ error.title }}</p>

    <Button type="submit" class="w-full" :disabled="isSubmitting">
      {{ isSubmitting ? '送出中...' : '寄送重設密碼信件' }}
    </Button>

    <RouterLink to="/login" class="block text-center text-sm text-muted-foreground hover:underline">
      返回登入
    </RouterLink>
  </form>

  <p v-else class="text-card-foreground">{{ forgotPasswordResult.message }}</p>
</template>

<style scoped></style>
