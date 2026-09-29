<template>
  <header class="border-b bg-card">
    <nav class="mx-auto flex max-w-4xl items-center gap-6 px-4 py-3">
      <span class="text-base font-semibold text-card-foreground">管理後台</span>
      <RouterLink to="/applications" class="text-sm text-muted-foreground hover:text-foreground">應用程式</RouterLink>
      <RouterLink to="/audit-logs" class="text-sm text-muted-foreground hover:text-foreground">稽核紀錄</RouterLink>
    </nav>
  </header>
</template>
