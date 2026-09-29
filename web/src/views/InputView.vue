<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { showSuccessToast, showToast } from 'vant'
import {
  entryApi,
  type BatchParseResponse,
  type EntryDto,
  type MissingField,
} from '../api'

const rawText = ref('')
const submitting = ref(false)
const history = ref<EntryDto[]>([])

// 最近一次批量结果摘要（用于提示多少条入账/待确认）
const lastSummary = ref('')

// 追问确认弹层状态（批量场景：待确认的追问逐个排队弹出）
const showConfirm = ref(false)
const confirmState = reactive<{
  draftToken: string | null
  questions: MissingField[]
  draftType: string
  answers: Record<string, string>
  hint: string
}>({
  draftToken: null,
  questions: [],
  draftType: 'note',
  answers: {},
  hint: '',
})
const pendingConfirm = ref<{ token: string; questions: MissingField[]; type: string; hint: string }[]>([])

async function loadHistory() {
  history.value = await entryApi.list({})
}

onMounted(loadHistory)

async function submit() {
  const text = rawText.value.trim()
  if (!text || submitting.value) return
  submitting.value = true
  try {
    const res: BatchParseResponse = await entryApi.batch(text)
    rawText.value = ''
    handleBatch(res)
    await loadHistory()
  } finally {
    submitting.value = false
  }
}

function handleBatch(res: BatchParseResponse) {
  const rows = res.batchResults ?? []
  if (rows.length === 0) {
    showToast('没听懂，换个说法试试')
    return
  }

  let ok = 0
  let failed = 0
  const missingQueue: { token: string; questions: MissingField[]; type: string; hint: string }[] = []

  for (const r of rows) {
    if (!r.needsConfirm) {
      ok++
    } else if (r.reason === 'missing' && r.draftToken) {
      missingQueue.push({
        token: r.draftToken,
        questions: r.questions ?? [],
        type: r.draft?.type ?? 'note',
        hint: r.hint ?? '',
      })
    } else {
      // parse_failed：原样挂起，已在列表可见
      failed++
    }
  }

  lastSummary.value = `共 ${rows.length} 条：已入账 ${ok} 条${missingQueue.length ? `，待确认 ${missingQueue.length} 条` : ''}${failed ? `，挂起 ${failed} 条` : ''}`
  showToast(lastSummary.value)

  if (missingQueue.length > 0) {
    pendingConfirm.value = missingQueue
    openNextConfirm()
    loadHistory()
  }
}

function openNextConfirm() {
  const next = pendingConfirm.value.shift()
  if (!next) {
    showConfirm.value = false
    return
  }
  confirmState.draftToken = next.token
  confirmState.questions = next.questions
  confirmState.draftType = next.type
  confirmState.answers = {}
  confirmState.hint = next.hint
  showConfirm.value = true
}

async function confirmSubmit() {
  const answers = Object.entries(confirmState.answers)
    .filter(([, v]) => v.trim())
    .map(([field, answer]) => ({ field, answer }))

  const res = await entryApi.confirm({
    draftToken: confirmState.draftToken ?? undefined,
    answers,
  })

  if (res.needsConfirm && res.reason === 'missing') {
    // 还差信息，继续追问这一条
    confirmState.questions = res.questions ?? []
    confirmState.draftToken = res.draftToken
    confirmState.answers = {}
    confirmState.hint = res.hint ?? ''
    return
  }

  showSuccessToast('已入账')
  // 确认完毕，进入下一条待确认项（如有）
  openNextConfirm()
  await loadHistory()
}
</script>

<template>
  <div class="input-view">
    <van-nav-bar title="说一句，记一笔" />

    <div class="input-card">
      <van-field
        v-model="rawText"
        type="textarea"
        rows="4"
        autosize
        maxlength="500"
        show-word-limit
        placeholder="像跟秘书说话一样，直接说，例如：&#10;今天进了200块的菜，卖了350，中午吃饭花了15"
      />
      <van-button
        round block type="primary"
        :loading="submitting"
        :disabled="!rawText.trim()"
        @click="submit"
      >
        说出来，记进去
      </van-button>
      <p class="tip">支持：收入支出、日程安排、重要事项、零散备忘。会自动分类成账本 / 日程 / 知识库。</p>
    </div>

    <div class="history">
      <van-cell-group inset title="最近记录">
        <van-cell v-for="e in history.slice(0, 10)" :key="e.id" :title="e.title"
          :label="`${e.occurredAt ?? e.createdAt} ${e.category ?? ''}`"
          :value="e.type === 'income' ? `+${e.amount}元` : e.type === 'expense' ? `-${e.amount}元` : e.type === 'event' ? '日程' : '备忘'" />
        <van-empty v-if="history.length === 0" description="还没有记录，说一句试试" image-size="80" />
      </van-cell-group>
    </div>

    <!-- 追问确认弹层（多条缺信息时逐条弹出） -->
    <van-popup v-model:show="showConfirm" round position="bottom" :style="{ padding: '20px 16px' }">
      <h3 class="q-title">还差几句话，帮我补一下</h3>
      <p class="q-hint">{{ confirmState.hint }}</p>
      <van-field
        v-for="q in confirmState.questions"
        :key="q.field"
        v-model="confirmState.answers[q.field]"
        :label="q.question"
        placeholder="在这里回答"
        class="q-field"
      />
      <van-button round block type="primary" @click="confirmSubmit">补齐，入账</van-button>
    </van-popup>
  </div>
</template>

<style scoped>
.input-card { padding: 16px; }
.input-card :deep(.van-field) {
  background: #fff;
  border-radius: 8px;
  margin-bottom: 12px;
  padding: 12px;
}
.tip { color: #999; font-size: 12px; padding: 8px 4px 0; }
.history { padding: 8px 0 16px; }
.q-title { margin: 0 0 4px; color: #4a3728; }
.q-hint { color: #999; font-size: 12px; margin: 0 0 12px; }
.q-field { margin-bottom: 10px; }
</style>