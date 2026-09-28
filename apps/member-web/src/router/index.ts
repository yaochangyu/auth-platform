import { createRouter, createWebHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/register' },
    { path: '/register', component: () => import('@/views/RegisterView.vue') },
    { path: '/verify-email', component: () => import('@/views/VerifyEmailView.vue') },
  ],
})
