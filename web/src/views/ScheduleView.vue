<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { entryApi, tagApi, type EntryDto, type TagDto } from '../api'

const list = ref<EntryDto[]>([])
const keyword = ref('')
const searchMode = ref<'exact' | 'ai'>('exact')

// 条目到标签的映射（按需并行拉取，避免 N+1 串行）
const entryTags = ref<Record<number, TagDto[]>>({})

async function load() {
  const kw = keyword.value.trim()
  const rows = searchMode.value === 'ai' && kw
    ? await entryApi.search(kw)
    : await entryApi.list({ type: 'event', q: kw || undefined })
  list.value = rows

  const settled = await Promise.allSettled(rows.map((e) => tagApi.getEntryTags(e.id)))
  const map: Record<number, TagDto[]> = {}
  rows.forEach((e, i) => {
    if (settled[i].status === 'fulfilled') map[e.id] = settled[i].value
  })
  entryTags.value = map
}

onMounted(load)
</script>

<template>
  <div>
    <van-nav-bar title="日程" />

    <van-search v-model="keyword" placeholder="搜索日程…" @search="load" />
    <div class="mode-bar">
      <span :class="['mode', { on: searchMode === 'exact' }]" @click="searchMode = 'exact'">精确匹配</span>
      <span :class="['mode', { on: searchMode === 'ai' }]" @click="searchMode = 'ai'">AI 智能搜索</span>
    </div>

    <van-cell-group inset>
      <van-cell v-for="e in list" :key="e.id" :title="e.title"
        :value="e.occurredAt ?? ''" icon="clock-o">
        <template #label>
          <span>{{ e.summary ?? '' }}</span>
          <span v-if="entryTags[e.id]?.length" class="tags">
            <van-tag v-for="t in entryTags[e.id]" :key="t.id" type="primary" plain class="tag">{{ t.name }}</van-tag>
          </span>
        </template>
      </van-cell>
      <van-empty v-if="list.length === 0" description="还没有日程，去口述页说一句试试" image-size="80" />
    </van-cell-group>
  </div>
</template>

<style scoped>
.tags { display: block; margin-top: 4px; }
.tag { margin-right: 6px; }
.mode-bar { display: flex; gap: 16px; padding: 0 16px 8px; align-items: center; }
.mode { color: #999; font-size: 13px; }
.mode.on { color: #b3541e; font-weight: 600; border-bottom: 2px solid #ffc940; }
</style>