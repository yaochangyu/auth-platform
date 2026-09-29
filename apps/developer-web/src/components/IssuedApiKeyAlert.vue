<script setup lang="ts">
import { ref, watch } from 'vue'
import { Button } from '@/components/ui/button'
import type { IssuedApiKey } from '@/types/apiKey'

const props = defineProps<{ issued: IssuedApiKey }>()

const emit = defineEmits<{
  dismiss: []
}>()

const copied = ref<'key' | 'secret' | null>(null)
const copyFailed = ref(false)

// 再次發行時，先前的「已複製」不能沿用到新的這一組。
watch(() => props.issued.id, () => {
  copied.value = null
  copyFailed.value = false
})

async function copy(text: string, which: 'key' | 'secret') {
  try {
    await navigator.clipboard.writeText(text)
    copied.value = which
  } catch {
    copyFailed.value = true
  }
}
</script>

<template>
  <div class="space-y-3 rounded-lg border border-destructive/50 bg-destructive/5 p-4" role="alert">
    <p class="text-sm font-medium text-destructive">
      請立即複製並妥善保存：API Key 與 API Secret 只會顯示這一次，關閉後無法再查看。
    </p>

    <div class="space-y-1">
      <p class="text-xs text-muted-foreground">API Key（放在 X-Api-Key 標頭）</p>
      <code class="block break-all rounded-md bg-background p-2 text-sm">{{ issued.apiKey }}</code>
      <Button size="sm" @click="copy(issued.apiKey, 'key')">{{ copied === 'key' ? '已複製' : '複製 API Key' }}</Button>
    </div>

    <div class="space-y-1">
      <p class="text-xs text-muted-foreground">API Secret（只用來計算 HMAC 簽章，不要放進請求）</p>
      <code class="block break-all rounded-md bg-background p-2 text-sm">{{ issued.apiSecret }}</code>
      <Button size="sm" @click="copy(issued.apiSecret, 'secret')">{{ copied === 'secret' ? '已複製' : '複製 API Secret' }}</Button>
    </div>

    <p v-if="copyFailed" class="text-sm text-destructive">無法自動複製，請手動選取上方內容複製。</p>
    <Button size="sm" variant="outline" @click="emit('dismiss')">我已保存，關閉</Button>
  </div>
</template>

<style scoped></style>
