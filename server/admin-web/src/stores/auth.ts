import { defineStore } from 'pinia'
import { authApi, type AdminProfile } from '@/api'
import { TOKEN_KEY } from '@/api/http'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem(TOKEN_KEY) || '',
    profile: null as AdminProfile | null,
  }),
  actions: {
    async login(username: string, password: string) {
      const res = await authApi.login(username, password)
      this.token = res.token
      localStorage.setItem(TOKEN_KEY, res.token)
      await this.fetchProfile()
    },
    async fetchProfile() {
      if (!this.token) return
      this.profile = await authApi.profile()
    },
    async changePassword(oldPassword: string, newPassword: string) {
      await authApi.changePassword(oldPassword, newPassword)
    },
    logout() {
      this.token = ''
      this.profile = null
      localStorage.removeItem(TOKEN_KEY)
    },
  },
})
