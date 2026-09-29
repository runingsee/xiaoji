<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { entryApi, tagApi, type EntryDto, type MonthSummary, type TagDto } from '../api'
import { useRouter } from 'vue-router'

const router = useRouter()
const tab = ref<'all' | 'expense' | 'income'>('all')
const list = ref<EntryDto[]>([])
const summary = ref<MonthSummary | null>(null)
const activeMonth = ref(new Date().toISOString().slice(0, 7))

const tags = ref<TagDto[]>([])
const selectedTagId = ref<number>(0)
const tagOptions = computed(() => [
  { text: '全部标签', value: 0 },
  ...tags.value.map((t) => ({ text: t.name, value: t.id })),
])

// 搜索：精确匹配走 entries 列表参数，AI 模式走语义搜索接口
const keyword = ref('')
const searchMode = ref<'exact' | 'ai'>('exact')

async function load() {
  const type = tab.value === 'all' ? undefined : tab.value
  const tagId = selectedTagId.value || undefined
  const [rows, s] = await Promise.all([
    entryApi.list({ type, month: activeMonth.value, tagId, q: keyword.value.trim() || undefined }),
    entryApi.summary(activeMonth.value),
  ])
  list.value = rows
  summary.value = s
}

async function doSearch() {
  const kw = keyword.value.trim()
  if (!kw) return load()
  if (searchMode.value === 'ai') {
    list.value = await entryApi.search(kw)
  } else {
    await load()
  }
}

onMounted(async () => {
  tags.value = await tagApi.list()
  await load()
})
</script>

<template>
  <div>
    <van-nav-bar title="账本" :right-text="`问粒粒`" @click-right="router.push('/ask')" />

    <!-- 搜索框：精确 / AI 模式切换 -->
    <van-search v-model="keyword" placeholder="搜索账目…" @search="doSearch" />
    <div class="mode-bar">
      <span :class="['mode', { on: searchMode === 'exact' }]" @click="searchMode = 'exact'">精确匹配</span>
      <span :class="['mode', { on: searchMode === 'ai' }]" @click="searchMode = 'ai'">AI 智能搜索</span>
    </div>

    <!-- 月度汇总卡片 -->
    <van-cell-group inset class="summary">
      <van-cell v-if="summary">
        <template #title>
          <span class="sum-label">收入</span>
          <span class="sum-amount income">+{{ summary.income.toFixed(2) }}</span>
          <span class="sum-label">支出</span>
          <span class="sum-amount expense">-{{ summary.expense.toFixed(2) }}</span>
          <span class="sum-label">结余</span>
          <span class="sum-amount">{{ summary.balance.toFixed(2) }}</span>
        </template>
        <template #value>
          <span class="month">{{ activeMonth }} 元</span>
        </template>
      </van-cell>
    </van-cell-group>

    <!-- 标签筛选 -->
    <div class="tag-filter">
      <van-dropdown-menu>
        <van-dropdown-item v-model="selectedTagId" :options="tagOptions" @change="load" />
      </van-dropdown-menu>
    </div>

    <div class="tabs">
      <van-tabs v-model:active="tab" @change="load">
        <van-tab title="全部" name="all" />
        <van-tab title="支出" name="expense" />
        <van-tab title="收入" name="income" />
      </van-tabs>
    </div>

    <van-cell-group inset>
      <van-cell v-for="e in list" :key="e.id" :title="e.title"
        :label="`${e.occurredAt ?? ''} ${e.category ?? ''}`"
        :value="e.type === 'income' ? `+${e.amount}元` : `-${e.amount}元`" />
      <van-empty v-if="list.length === 0" description="这个月还没有账目" image-size="80" />
    </van-cell-group>
  </div>
</template>

<style scoped>
.summary { margin: 12px 16px 0; }
.sum-label { color: #999; font-size: 13px; margin: 0 4px 0 12px; }
.sum-amount { font-weight: 600; }
.sum-amount.income { color: #e0762c; }
.sum-amount.expense { color: #b3541e; }
.month { color: #999; font-size: 12px; }
.tag-filter { margin-top: 8px; }
.tabs { padding: 12px 0 4px; }
.mode-bar { display: flex; gap: 16px; padding: 0 16px 8px; align-items: center; }
.mode { color: #999; font-size: 13px; }
.mode.on { color: #b3541e; font-weight: 600; border-bottom: 2px solid #ffc940; }
</style>