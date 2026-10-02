<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { useAuth } from '@/composables/useAuth'
import { useAuthStore } from '@/stores/auth'

const { logoutAndRedirect } = useAuth()
// 登入後整頁重載會清空 loginResult；以 Auth Guard 載入的 profile 判斷登入狀態
const store = useAuthStore()
</script>

<template>
  <header class="flex items-center justify-between border-b bg-card px-4 py-3">
    <span class="font-semibold text-card-foreground">會員中心</span>

    <div v-if="store.profile" class="flex items-center gap-3">
      <RouterLink to="/member" class="text-sm text-muted-foreground hover:underline">
        {{ store.profile.email }}
      </RouterLink>
      <Button variant="outline" size="sm" @click="logoutAndRedirect">登出</Button>
    </div>
  </header>
</template>

<style scoped></style>
