<script setup lang="ts">
import RegisterForm from '@/components/RegisterForm.vue'
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-background px-4">
    <div class="w-full max-w-sm rounded-lg border bg-card p-8 shadow-sm">
      <h1 class="text-2xl font-semibold text-card-foreground">會員註冊</h1>
      <div class="mt-6">
        <RegisterForm />
      </div>
    </div>
  </main>
</template>

<style scoped></style>
