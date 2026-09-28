<script setup lang="ts">
import { Button } from '@/components/ui/button'

const appName = '會員中心'
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-background">
    <div class="rounded-lg border bg-card p-8 text-center shadow-sm">
      <h1 class="text-2xl font-semibold text-card-foreground">{{ appName }}</h1>
      <p class="mt-2 text-sm text-muted-foreground">member-web 專案骨架已就緒</p>
      <Button class="mt-4">開始使用</Button>
    </div>
  </main>
</template>

<style scoped></style>
