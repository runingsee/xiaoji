<script setup lang="ts">
import { reactive, ref } from 'vue'
import { showSuccessToast } from 'vant'
import { authApi } from '../api'
import { setAuth } from '../store/auth'
import { useRouter } from 'vue-router'

const router = useRouter()
const mode = ref<'login' | 'register'>('login')
const loading = ref(false)
const form = reactive({ phone: '', password: '', nickname: '' })

async function submit() {
  if (!form.phone || !form.password) return
  loading.value = true
  try {
    const res: any =
      mode.value === 'login'
        ? await authApi.login({ phone: form.phone, password: form.password })
        : await authApi.register({ phone: form.phone, password: form.password, nickname: form.nickname })
    setAuth(res.token, res.user)
    showSuccessToast(mode.value === 'login' ? '欢迎回来' : '注册成功')
    router.replace('/')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-wrap">
    <div class="brand">
      <div class="logo">🐤</div>
      <h1>晓记</h1>
      <p>你只管说，剩下的交给它</p>
    </div>

    <van-form @submit="submit">
      <van-cell-group inset>
        <van-field
          v-model="form.phone"
          name="phone"
          label="手机号"
          placeholder="用于登录"
          type="tel"
          maxlength="11"
          :rules="[{ required: true, message: '请输入手机号' }]"
        />
        <van-field
          v-model="form.nickname"
          v-if="mode === 'register'"
          name="nickname"
          label="昵称"
          placeholder="怎么称呼你（可选）"
        />
        <van-field
          v-model="form.password"
          name="password"
          label="密码"
          type="password"
          placeholder="至少 6 位"
          :rules="[{ required: true, message: '请输入密码' }]"
        />
      </van-cell-group>

      <div class="actions">
        <van-button round block type="primary" native-type="submit" :loading="loading">
          {{ mode === 'login' ? '登录' : '注册' }}
        </van-button>
        <van-button plain round block class="switch" @click="mode = mode === 'login' ? 'register' : 'login'">
          {{ mode === 'login' ? '没有账号？注册一个' : '已有账号？去登录' }}
        </van-button>
      </div>
    </van-form>
  </div>
</template>

<style scoped>
.login-wrap { padding: 24px 16px; }
.brand { text-align: center; margin: 48px 0 32px; }
.logo { font-size: 56px; }
.brand h1 { margin: 8px 0 4px; color: #4a3728; }
.brand p { margin: 0; color: #8c8c8c; font-size: 14px; }
.actions { margin-top: 24px; padding: 0 16px; }
.switch { margin-top: 12px; color: #4a3728; }
</style>