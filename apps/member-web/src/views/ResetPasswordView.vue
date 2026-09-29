<script setup lang="ts">
import ResetPasswordForm from '@/components/ResetPasswordForm.vue'
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-background px-4">
    <div class="w-full max-w-sm rounded-lg border bg-card p-8 shadow-sm">
      <h1 class="text-2xl font-semibold text-card-foreground">重設密碼</h1>
      <div class="mt-6">
        <ResetPasswordForm />
      </div>
    </div>
  </main>
</template>

<style scoped></style>
