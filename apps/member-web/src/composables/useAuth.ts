import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'

// 狀態與動作都委派給 Pinia 的 useAuthStore（單一真實來源），這裡只保留元件慣用的名稱。
export function useAuth() {
  const store = useAuthStore()
  const { isSubmitting, error, registerResult, loginResult } = storeToRefs(store)

  return {
    isSubmitting,
    error,
    result: registerResult,
    loginResult,
    register: store.register,
    login: store.login,
    loginAndRedirect: store.loginAndRedirect,
    logout: store.logout,
    logoutAndRedirect: store.logoutAndRedirect,
  }
}
