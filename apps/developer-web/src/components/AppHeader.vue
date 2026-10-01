<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <header class="border-b bg-card">
    <nav class="mx-auto flex max-w-3xl items-center justify-between px-4 py-3">
      <RouterLink to="/apps" class="text-base font-semibold text-card-foreground">開發者後台</RouterLink>

      <div v-if="auth.isAuthenticated" class="flex items-center gap-3">
        <span class="text-sm text-muted-foreground">{{ auth.displayIdentity }}</span>
        <Button variant="outline" size="sm" @click="auth.logout()">登出</Button>
      </div>
    </nav>
  </header>
</template>
