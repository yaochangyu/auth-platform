import { useOAuth } from '@/composables/useOAuth'
import { useAuthStore } from '@/stores/auth'
import type { ProblemDetails } from '@/types/problem'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

export async function apiRequest(path: string, init: RequestInit = {}): Promise<Response> {
  const auth = useAuthStore()
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      ...(init.body ? { 'Content-Type': 'application/json' } : {}),
      Authorization: `Bearer ${auth.accessToken}`,
    },
  })

  // Access Token 過期或被撤銷：清除後走 SSO 無感重新登入，回到目前頁面。
  const { justLoggedIn, startLogin } = useOAuth()
  if (response.status === 401 && !justLoggedIn()) {
    auth.clear()
    await startLogin(window.location.pathname + window.location.search)
  }

  return response
}

// 非 JSON 的錯誤回應（例如 Proxy 的 502）不能讓畫面崩潰。
export async function problemOf(response: Response): Promise<ProblemDetails> {
  return response
    .json()
    .then((problem) => problem as ProblemDetails)
    .catch(() => ({ title: '發生未預期的錯誤，請稍後再試。', status: response.status }))
}
