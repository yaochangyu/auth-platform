import { defineStore } from 'pinia'
import type { MemberProfileResponse } from '@/types/auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

export const useAuthStore = defineStore('auth', {
  state: () => ({
    profile: null as MemberProfileResponse | null,
    isLoading: false,
  }),

  getters: {
    isAuthenticated: (state) => state.profile !== null,
  },

  actions: {
    // 回傳是否成功，供 Router Auth Guard 判斷要不要放行。
    async fetchProfile(): Promise<boolean> {
      this.isLoading = true

      try {
        const response = await fetch(`${API_BASE}/api/v1/member/profile`, {
          credentials: 'include',
        })

        if (response.ok) {
          this.profile = (await response.json()) as MemberProfileResponse
          return true
        }

        this.profile = null
        return false
      } finally {
        this.isLoading = false
      }
    },

    clear() {
      this.profile = null
    },
  },
})
