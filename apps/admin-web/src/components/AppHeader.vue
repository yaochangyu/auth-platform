<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <header class="border-b bg-card">
    <nav class="mx-auto flex max-w-4xl items-center justify-between px-4 py-3">
      <div class="flex items-center gap-6">
        <span class="text-base font-semibold text-card-foreground">管理後台</span>
        <RouterLink to="/applications" class="text-sm text-muted-foreground hover:text-foreground">應用程式</RouterLink>
        <RouterLink to="/audit-logs" class="text-sm text-muted-foreground hover:text-foreground">稽核紀錄</RouterLink>
      </div>

      <div v-if="auth.isAuthenticated" class="flex items-center gap-3">
        <span class="text-sm text-muted-foreground">{{ auth.displayIdentity }}</span>
        <Button variant="outline" size="sm" @click="auth.logout()">登出</Button>
      </div>
    </nav>
  </header>
</template>
