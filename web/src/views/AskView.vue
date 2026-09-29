<script setup lang="ts">
import { ref } from 'vue'
import { askApi, type AskResponse } from '../api'

interface Msg {
  role: 'user' | 'bot'
  text: string
}

const input = ref('')
const loading = ref(false)
const msgs = ref<Msg[]>([
  { role: 'bot', text: '我是粒粒，问我账本、日程、记过的事，比如「这个月赚了多少」' },
])

const quickQuestions = ['这个月赚了多少', '花了多少钱', '最近有什么安排', '记过老王的事吗']

async function send(text?: string) {
  const q = (text ?? input.value).trim()
  if (!q || loading.value) return
  input.value = ''
  msgs.value.push({ role: 'user', text: q })
  loading.value = true
  try {
    const res: AskResponse = await askApi.ask(q)
    msgs.value.push({ role: 'bot', text: res.answer })
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="ask-view">
    <van-nav-bar title="问粒粒" />

    <div class="chat">
      <div v-for="(m, i) in msgs" :key="i" :class="['msg', m.role]">
        <div class="bubble">{{ m.text }}</div>
      </div>

      <div class="quick" v-if="msgs.length <= 1">
        <van-button v-for="q in quickQuestions" :key="q" size="small" round class="q-btn" @click="send(q)">
          {{ q }}
        </van-button>
      </div>
    </div>

    <div class="input-bar">
      <van-field v-model="input" placeholder="问点什么…" @keyup.enter="send()" />
      <van-button :loading="loading" round type="primary" @click="send()">问</van-button>
    </div>
  </div>
</template>

<style scoped>
.ask-view { display: flex; flex-direction: column; height: calc(100vh - 60px); }
.chat { flex: 1; overflow-y: auto; padding: 16px; }
.msg { margin-bottom: 12px; }
.msg.user { text-align: right; }
.bubble {
  display: inline-block; max-width: 80%;
  padding: 10px 12px; border-radius: 12px;
  background: #fff; color: #4a3728; text-align: left;
}
.msg.user .bubble { background: #ffc940; color: #4a3728; }
.quick { margin-top: 12px; }
.q-btn { margin: 0 8px 8px 0; }
.input-bar { display: flex; gap: 8px; padding: 10px 12px; background: #fff; }
</style>