<script setup lang="ts">
import { reactive } from 'vue'
import { useRoute } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { usePasswordReset } from '@/composables/usePasswordReset'

const route = useRoute()

const form = reactive({
  newPassword: '',
  confirmPassword: '',
})

const { isSubmitting, error, resetPasswordResult, resetPassword } = usePasswordReset()

function onSubmit() {
  const verificationToken = typeof route.query.token === 'string' ? route.query.token : ''
  resetPassword({ verificationToken, ...form })
}
</script>

<template>
  <form v-if="!resetPasswordResult" class="space-y-4" @submit.prevent="onSubmit">
    <div class="space-y-2">
      <Label for="newPassword">新密碼</Label>
      <Input id="newPassword" v-model="form.newPassword" type="password" required />
      <p v-if="error?.errors?.newPassword" class="text-sm text-destructive">
        {{ error.errors.newPassword[0] }}
      </p>
    </div>

    <div class="space-y-2">
      <Label for="confirmPassword">確認新密碼</Label>
      <Input id="confirmPassword" v-model="form.confirmPassword" type="password" required />
      <p v-if="error?.errors?.confirmPassword" class="text-sm text-destructive">
        {{ error.errors.confirmPassword[0] }}
      </p>
    </div>

    <p v-if="error && !error.errors" class="text-sm text-destructive">{{ error.title }}</p>

    <Button type="submit" class="w-full" :disabled="isSubmitting">
      {{ isSubmitting ? '送出中...' : '重設密碼' }}
    </Button>
  </form>

  <div v-else class="space-y-4">
    <p class="text-card-foreground">{{ resetPasswordResult.message }}</p>
    <RouterLink to="/login" class="block text-center text-sm text-muted-foreground hover:underline">
      前往登入
    </RouterLink>
  </div>
</template>

<style scoped></style>
