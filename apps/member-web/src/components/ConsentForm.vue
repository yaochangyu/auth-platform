<script setup lang="ts">
import { onMounted } from 'vue'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { useConsent } from '@/composables/useConsent'

const props = defineProps<{ consentId: string }>()

const { isLoading, error, details, selectedScopes, submitUrl, fetchDetails, isRequired } = useConsent(
  props.consentId,
)

onMounted(fetchDetails)
</script>

<template>
  <p v-if="isLoading" class="text-sm text-muted-foreground">載入中...</p>
  <p v-else-if="error" class="text-sm text-destructive">{{ error.title }}</p>

  <form v-else-if="details" method="post" :action="submitUrl" class="space-y-6">
    <p class="text-sm text-card-foreground">
      <strong>{{ details.applicationName }}</strong> 想要存取您的下列資料：
    </p>

    <ul class="space-y-3">
      <li v-for="scope in details.scopes" :key="scope.name" class="flex items-start gap-3">
        <input
          :id="`scope-${scope.name}`"
          v-model="selectedScopes"
          type="checkbox"
          name="scope"
          :value="scope.name"
          :disabled="isRequired(scope.name)"
          class="mt-1 size-4 accent-primary"
        />
        <input v-if="isRequired(scope.name)" type="hidden" name="scope" :value="scope.name" />
        <Label :for="`scope-${scope.name}`">{{ scope.description }}</Label>
      </li>
    </ul>

    <div class="flex gap-3">
      <Button type="submit" name="decision" value="deny" variant="outline" class="flex-1">拒絕</Button>
      <Button type="submit" name="decision" value="approve" class="flex-1">同意</Button>
    </div>
  </form>
</template>

<style scoped></style>
