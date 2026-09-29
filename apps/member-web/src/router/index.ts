import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/login' },
    { path: '/login', component: () => import('@/views/LoginView.vue') },
    { path: '/register', component: () => import('@/views/RegisterView.vue') },
    { path: '/verify-email', component: () => import('@/views/VerifyEmailView.vue') },
    { path: '/forgot-password', component: () => import('@/views/ForgotPasswordView.vue') },
    { path: '/reset-password', component: () => import('@/views/ResetPasswordView.vue') },
    {
      path: '/member',
      component: () => import('@/views/MemberProfileView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/connected-apps',
      component: () => import('@/views/ConnectedAppsView.vue'),
      meta: { requiresAuth: true },
    },
  ],
})

// Auth Guard：受保護路由先向後端確認 Session Cookie 仍有效（GET /api/v1/member/profile），
// 不通過就導回登入頁並帶上 returnUrl，讓登入成功後能導回原本想去的頁面。
router.beforeEach(async (to) => {
  if (!to.meta.requiresAuth) {
    return true
  }

  const store = useAuthStore()
  const ok = await store.fetchProfile()
  if (!ok) {
    return { path: '/login', query: { returnUrl: to.fullPath } }
  }

  return true
})
