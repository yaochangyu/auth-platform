import { createRouter, createWebHistory } from 'vue-router'
import { useOAuth } from '@/composables/useOAuth'
import { useAuthStore } from '@/stores/auth'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/applications' },
    { path: '/oauth/callback', component: () => import('@/views/OAuthCallbackView.vue') },
    { path: '/applications', component: () => import('@/views/ApplicationListView.vue'), meta: { requiresAuth: true } },
    { path: '/applications/:applicationId', component: () => import('@/views/ApplicationDetailView.vue'), meta: { requiresAuth: true } },
    { path: '/audit-logs', component: () => import('@/views/AuditLogsView.vue'), meta: { requiresAuth: true } },
  ],
})

// Auth Guard：沒有有效 Access Token 就轉往 auth-server 登入（Dogfooding），成功後回到原本要去的頁面。
// 是否為管理員由 admin-api 判定（非管理員一律 403），前端只負責顯示。
router.beforeEach(async (to) => {
  if (!to.meta.requiresAuth || useAuthStore().isAuthenticated) {
    return true
  }

  await useOAuth().startLogin(to.fullPath)
  return false
})
