import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'

// 登入生命週期都在 useAuthStore，這裡只保留呼叫端慣用的入口。
export function useOAuth() {
  const store = useAuthStore()
  const { error } = storeToRefs(store)

  return { error, startLogin: store.startLogin, completeLogin: store.completeLogin, justLoggedIn: store.justLoggedIn }
}
