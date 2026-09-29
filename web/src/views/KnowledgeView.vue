<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { entryApi, tagApi, type EntryDto, type TagDto } from '../api'

const list = ref<EntryDto[]>([])
const keyword = ref('')
const searchMode = ref<'exact' | 'ai'>('exact')
const tags = ref<TagDto[]>([])
const selectedTagId = ref<number>(0)
const tagOptions = computed(() => [
  { text: '全部标签', value: 0 },
  ...tags.value.map((t) => ({ text: t.name, value: t.id })),
])

async function load() {
  const tagId = selectedTagId.value || undefined
  list.value = await entryApi.list({ type: 'note', q: keyword.value || undefined, tagId })
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
    <van-nav-bar title="知识库" />

    <van-search v-model="keyword" placeholder="搜索记过的零散事" @search="doSearch" />
    <div class="mode-bar">
      <span :class="['mode', { on: searchMode === 'exact' }]" @click="searchMode = 'exact'">精确匹配</span>
      <span :class="['mode', { on: searchMode === 'ai' }]" @click="searchMode = 'ai'">AI 智能搜索</span>
    </div>

    <div class="tag-filter">
      <van-dropdown-menu>
        <van-dropdown-item v-model="selectedTagId" :options="tagOptions" @change="load" />
      </van-dropdown-menu>
    </div>

    <van-cell-group inset>
      <van-cell v-for="e in list" :key="e.id" :title="e.title"
        :label="`${e.occurredAt ?? e.createdAt} ${e.rawText.slice(0, 30)}`" />
      <van-empty v-if="list.length === 0" description="还没有零散备忘" image-size="80" />
    </van-cell-group>
  </div>
</template>

<style scoped>
.tag-filter { margin-top: 4px; }
.mode-bar { display: flex; gap: 16px; padding: 0 16px 8px; align-items: center; }
.mode { color: #999; font-size: 13px; }
.mode.on { color: #b3541e; font-weight: 600; border-bottom: 2px solid #ffc940; }
</style>