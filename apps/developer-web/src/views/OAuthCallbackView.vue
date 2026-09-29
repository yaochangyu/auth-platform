<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { useOAuth } from '@/composables/useOAuth'

const route = useRoute()
const router = useRouter()
const { error, startLogin, completeLogin } = useOAuth()

onMounted(async () => {
  const returnPath = await completeLogin(route.query)
  if (returnPath) {
    await router.replace(returnPath)
  }
})
</script>

<template>
  <main class="flex min-h-[60vh] items-center justify-center px-4">
    <div class="w-full max-w-sm space-y-4 text-center">
      <p v-if="!error" class="text-sm text-muted-foreground">登入中...</p>
      <template v-else>
        <p class="text-sm text-destructive">{{ error }}</p>
        <Button @click="startLogin('/apps')">重新登入</Button>
      </template>
    </div>
  </main>
</template>

<style scoped></style>
