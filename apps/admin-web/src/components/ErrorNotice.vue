<script setup lang="ts">
import type { ProblemDetails } from '@/types/problem'

defineProps<{ error: ProblemDetails | null }>()
</script>

<template>
  <p v-if="error?.status === 403" class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive" role="alert">
    此帳號不是平台管理員，無法使用管理後台。
  </p>
  <p v-else-if="error" class="text-sm text-destructive">{{ error.title }}</p>
</template>
