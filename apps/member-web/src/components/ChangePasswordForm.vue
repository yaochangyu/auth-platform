<script setup lang="ts">
import { reactive } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useChangePassword } from '@/composables/useChangePassword'

const form = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
})

const { isSubmitting, error, result, changePassword } = useChangePassword()

function onSubmit() {
  changePassword({ ...form })
}
</script>

<template>
  <form v-if="!result" class="space-y-4" @submit.prevent="onSubmit">
    <div class="space-y-2">
      <Label for="currentPassword">目前密碼</Label>
      <Input id="currentPassword" v-model="form.currentPassword" type="password" required />
    </div>

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
      {{ isSubmitting ? '送出中...' : '更新密碼' }}
    </Button>
  </form>

  <p v-else class="text-card-foreground">{{ result.message }}</p>
</template>

<style scoped></style>
