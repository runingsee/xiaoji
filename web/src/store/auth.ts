import { reactive } from 'vue'

// 极简全局登录态（V1 不引入 Pinia，量级没必要）
export interface UserInfo {
  id: number
  phone: string
  nickname?: string | null
  plan: string
}

export const auth = reactive<{ token: string; user: UserInfo | null }>({
  token: localStorage.getItem('xiaoji_token') ?? '',
  user: null,
})

export function setAuth(token: string, user: UserInfo) {
  auth.token = token
  auth.user = user
  localStorage.setItem('xiaoji_token', token)
}

export function clearAuth() {
  auth.token = ''
  auth.user = null
  localStorage.removeItem('xiaoji_token')
}