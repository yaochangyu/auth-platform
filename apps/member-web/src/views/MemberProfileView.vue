<script setup lang="ts">
import { onMounted } from 'vue'
import ChangePasswordForm from '@/components/ChangePasswordForm.vue'
import { useAuthStore } from '@/stores/auth'

const store = useAuthStore()

onMounted(() => {
  if (!store.profile) {
    store.fetchProfile()
  }
})
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-background px-4 py-8">
    <div class="w-full max-w-md space-y-6 rounded-lg border bg-card p-8 shadow-sm">
      <div v-if="store.profile">
        <h1 class="text-2xl font-semibold text-card-foreground">會員中心</h1>
        <dl class="mt-4 space-y-2 text-sm">
          <div class="flex justify-between">
            <dt class="text-muted-foreground">Email</dt>
            <dd class="text-card-foreground">{{ store.profile.email }}</dd>
          </div>
          <div class="flex justify-between">
            <dt class="text-muted-foreground">暱稱</dt>
            <dd class="text-card-foreground">{{ store.profile.displayName }}</dd>
          </div>
          <div class="flex justify-between">
            <dt class="text-muted-foreground">狀態</dt>
            <dd class="text-card-foreground">{{ store.profile.status }}</dd>
          </div>
        </dl>
      </div>

      <div class="border-t pt-6">
        <h2 class="text-lg font-semibold text-card-foreground">修改密碼</h2>
        <div class="mt-4">
          <ChangePasswordForm />
        </div>
      </div>
    </div>
  </main>
</template>

<style scoped></style>
