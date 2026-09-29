import { createRouter, createWebHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/login' },
    { path: '/login', component: () => import('@/views/LoginView.vue') },
    { path: '/register', component: () => import('@/views/RegisterView.vue') },
    { path: '/verify-email', component: () => import('@/views/VerifyEmailView.vue') },
    { path: '/forgot-password', component: () => import('@/views/ForgotPasswordView.vue') },
    { path: '/reset-password', component: () => import('@/views/ResetPasswordView.vue') },
  ],
})
