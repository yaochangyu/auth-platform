<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { useAuth } from '@/composables/useAuth'

const { loginResult, logoutAndRedirect } = useAuth()
</script>

<template>
  <header class="flex items-center justify-between border-b bg-card px-4 py-3">
    <span class="font-semibold text-card-foreground">會員中心</span>

    <div v-if="loginResult" class="flex items-center gap-3">
      <span class="text-sm text-muted-foreground">{{ loginResult.email }}</span>
      <Button variant="outline" size="sm" @click="logoutAndRedirect">登出</Button>
    </div>
  </header>
</template>

<style scoped></style>
