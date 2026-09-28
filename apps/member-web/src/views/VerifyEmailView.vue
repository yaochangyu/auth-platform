<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useVerification } from '@/composables/useVerification'

const route = useRoute()
const { isVerifying, error, result, verifyEmail } = useVerification()

onMounted(() => {
  const token = route.query.token
  if (typeof token === 'string' && token.length > 0) {
    verifyEmail(token)
  }
})
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-background px-4">
    <div class="w-full max-w-sm rounded-lg border bg-card p-8 text-center shadow-sm">
      <h1 class="text-2xl font-semibold text-card-foreground">Email 驗證結果</h1>

      <p v-if="isVerifying" class="mt-4 text-muted-foreground">驗證中...</p>

      <div v-else-if="result" class="mt-4 space-y-2">
        <p class="text-card-foreground">{{ result.message }}</p>
        <p class="text-sm text-muted-foreground">會員狀態：{{ result.status }}</p>
      </div>

      <div v-else-if="error" class="mt-4 space-y-2">
        <p class="text-destructive">{{ error.title }}</p>
      </div>

      <p v-else class="mt-4 text-muted-foreground">缺少驗證權杖。</p>
    </div>
  </main>
</template>

<style scoped></style>
