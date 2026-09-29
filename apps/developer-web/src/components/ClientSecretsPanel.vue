<script setup lang="ts">
import { ref, watch } from 'vue'
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
import type { ClientSecretSummary, IssuedClientSecret } from '@/types/oauthClient'

const props = defineProps<{
  secrets: ClientSecretSummary[]
  issuedSecret: IssuedClientSecret | null
  isBusy: boolean
}>()

const emit = defineEmits<{
  issue: []
  revoke: [secretId: string]
  dismiss: []
}>()

const copied = ref(false)
const copyFailed = ref(false)

// 再次發行新 Secret 時，先前的「已複製」不能沿用到新的這一組。
watch(() => props.issuedSecret?.id, () => {
  copied.value = false
  copyFailed.value = false
})

async function copy(secret: string) {
  try {
    await navigator.clipboard.writeText(secret)
    copied.value = true
  } catch {
    copyFailed.value = true
  }
}

const statusLabel: Record<ClientSecretSummary['status'], string> = {
  Active: '使用中',
  Expiring: '過渡期（即將到期）',
  Expired: '已到期',
  Revoked: '已作廢',
}
</script>

<template>
  <section class="space-y-4">
    <div class="flex items-center justify-between">
      <h2 class="text-lg font-semibold text-foreground">Client Secret</h2>
      <Button size="sm" :disabled="isBusy" @click="emit('issue')">產生新 Secret</Button>
    </div>

    <p class="text-sm text-muted-foreground">
      產生新 Secret 時，目前使用中的 Secret 會進入 24 小時過渡期，期間新舊兩組都可以換發 Token；
      更新完成後可手動作廢舊的。同時最多兩組有效。
    </p>

    <div v-if="issuedSecret" class="space-y-3 rounded-lg border border-destructive/50 bg-destructive/5 p-4" role="alert">
      <p class="text-sm font-medium text-destructive">請立即複製並妥善保存：這組 Secret 只會顯示這一次，關閉後無法再查看。</p>
      <code class="block break-all rounded-md bg-background p-2 text-sm">{{ issuedSecret.secret }}</code>
      <p v-if="copyFailed" class="text-sm text-destructive">無法自動複製，請手動選取上方的 Secret 複製。</p>
      <div class="flex gap-2">
        <Button size="sm" @click="copy(issuedSecret.secret)">{{ copied ? '已複製' : '一鍵複製' }}</Button>
        <Button size="sm" variant="outline" @click="emit('dismiss')">我已保存，關閉</Button>
      </div>
    </div>

    <p v-if="secrets.length === 0" class="text-sm text-muted-foreground">尚未發行任何 Secret。</p>
    <ul v-else class="space-y-2">
      <li v-for="secret in secrets" :key="secret.id" class="flex items-center justify-between rounded-md border p-3">
        <div>
          <p class="font-mono text-sm">{{ secret.prefix }}…</p>
          <p class="text-xs text-muted-foreground">
            {{ statusLabel[secret.status] }}<template v-if="secret.expiresAt">，到期：{{ new Date(secret.expiresAt).toLocaleString() }}</template>
          </p>
        </div>

        <AlertDialog v-if="secret.status === 'Active' || secret.status === 'Expiring'">
          <AlertDialogTrigger as-child>
            <Button variant="outline" size="sm" :disabled="isBusy">作廢</Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>確定要作廢這組 Secret？</AlertDialogTitle>
              <AlertDialogDescription>作廢後立即失效，仍使用這組 Secret 的服務將無法換發 Token，此操作無法復原。</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>取消</AlertDialogCancel>
              <AlertDialogAction @click="emit('revoke', secret.id)">確定作廢</AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </li>
    </ul>
  </section>
</template>

<style scoped></style>
