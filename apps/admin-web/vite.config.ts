import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  // 開發時以同源代理呼叫 admin-api 與 auth-server 的 Token 端點，避免 CORS。
  server: {
    port: 5175,
    proxy: {
      '/api/v1/auth/logout': process.env.VITE_DEV_MEMBER_TARGET ?? 'http://localhost:5101',
      '/api': process.env.VITE_DEV_API_TARGET ?? 'http://localhost:5104',
      '/connect/token': process.env.VITE_DEV_AUTH_TARGET ?? 'http://localhost:5102',
      '/connect/userinfo': process.env.VITE_DEV_AUTH_TARGET ?? 'http://localhost:5102',
    },
  },
})
