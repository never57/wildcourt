import { defineStore } from 'pinia'
import { authApi, type AdminProfile } from '@/api'
import { TOKEN_KEY } from '@/api/http'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem(TOKEN_KEY) || '',
    profile: null as AdminProfile | null,
  }),
  getters: {
    permissions: (state) => state.profile?.permissions ?? [],
    role: (state) => state.profile?.role ?? '',
    twoFactorEnabled: (state) => state.profile?.twoFactorEnabled ?? false,
  },
  actions: {
    async login(data: { username: string; password: string; captchaId?: string; captchaCode?: string; totpCode?: string; totpTicket?: string }) {
      const res = await authApi.login(data)
      // 双因素第二步：token 为空且 needTotp=true 时不写入 token
      if (res.token) {
        this.token = res.token
        localStorage.setItem(TOKEN_KEY, res.token)
        await this.fetchProfile()
      }
      return res
    },
    /** 判断是否拥有指定权限（任一满足即可） */
    hasPermission(...perms: string[]) {
      const set = this.permissions
      return perms.length === 0 || perms.some((p) => set.includes(p))
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
