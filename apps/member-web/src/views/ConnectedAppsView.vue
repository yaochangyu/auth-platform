<script setup lang="ts">
import { onMounted } from 'vue'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { useConnectedApps } from '@/composables/useConnectedApps'

const { isLoading, isRevoking, error, apps, fetchApps, revokeApp } = useConnectedApps()

onMounted(fetchApps)
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-background px-4 py-8">
    <div class="w-full max-w-md space-y-6 rounded-lg border bg-card p-8 shadow-sm">
      <h1 class="text-2xl font-semibold text-card-foreground">已連結的應用程式</h1>

      <p v-if="error" class="text-sm text-destructive">{{ error.title }}</p>

      <p v-if="isLoading" class="text-sm text-muted-foreground">載入中...</p>

      <p v-else-if="apps.length === 0" class="text-sm text-muted-foreground">
        目前尚未授權任何第三方應用程式。
      </p>

      <ul v-else class="space-y-3">
        <li
          v-for="app in apps"
          :key="app.appId"
          class="flex items-center justify-between rounded-md border p-3"
        >
          <div>
            <p class="font-medium text-card-foreground">{{ app.appName }}</p>
            <p class="text-xs text-muted-foreground">
              授權時間：{{ new Date(app.authorizedAt).toLocaleString() }}
            </p>
          </div>

          <AlertDialog>
            <AlertDialogTrigger as-child>
              <Button variant="outline" size="sm" :disabled="isRevoking">解除連結</Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>確定要解除連結「{{ app.appName }}」？</AlertDialogTitle>
                <AlertDialogDescription>
                  解除後，此應用程式將無法再存取您的個人資料，此操作無法復原。
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>取消</AlertDialogCancel>
                <AlertDialogAction @click="revokeApp(app.appId)">確定解除</AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </li>
      </ul>
    </div>
  </main>
</template>

<style scoped></style>
