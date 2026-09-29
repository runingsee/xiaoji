<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { showConfirmDialog, showDialog, showToast } from 'vant'
import { tagApi, type TagDto, type TagStatsDto } from '../api'

const month = ref(new Date().toISOString().slice(0, 7))
const stats = ref<TagStatsDto[]>([])
const tags = ref<TagDto[]>([])
const showCreate = ref(false)
const newName = ref('')

const income = computed(() => stats.value.filter((s) => s.type === 'income'))
const expense = computed(() => stats.value.filter((s) => s.type === 'expense'))

async function load() {
  const [s, t] = await Promise.all([tagApi.stats(month.value), tagApi.list()])
  stats.value = s
  tags.value = t
}

function shiftMonth(delta: number) {
  const [y, m] = month.value.split('-').map(Number)
  const d = new Date(y, m - 1 + delta, 1)
  month.value = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
  load()
}

async function onCreate() {
  const name = newName.value.trim()
  if (!name) {
    showToast('标签名不能为空')
    return
  }
  await tagApi.create(name)
  newName.value = ''
  showCreate.value = false
  showToast('已创建')
  load()
}

function onDelete(t: TagDto) {
  showConfirmDialog({
    title: '删除标签',
    message: `确定删除「${t.name}」？记录不会被删除，但会解除关联。`,
  })
    .then(async () => {
      await tagApi.remove(t.id)
      showToast('已删除')
      load()
    })
    .catch(() => {})
}

onMounted(load)
</script>

<template>
  <div>
    <van-nav-bar title="标签">
      <template #right>
        <van-icon name="plus" size="20" color="#B3541E" @click="showCreate = true" />
      </template>
    </van-nav-bar>

    <!-- 月度统计 -->
    <van-cell-group inset class="stats">
      <van-cell>
        <template #title>
          <span class="month-label">{{ month }} 标签统计</span>
        </template>
        <template #value>
          <van-icon name="arrow-left" @click="shiftMonth(-1)" />
          <van-icon name="arrow" class="arrow-next" @click="shiftMonth(1)" />
        </template>
      </van-cell>
      <van-cell v-if="income.length > 0" title="收入">
        <template #value>
          <div class="stat-line" v-for="s in income" :key="`i-${s.tagName}`">
            <span>{{ s.tagName }}</span>
            <span>{{ s.count }} 笔 · {{ s.totalAmount.toFixed(2) }} 元</span>
          </div>
        </template>
      </van-cell>
      <van-cell v-if="expense.length > 0" title="支出">
        <template #value>
          <div class="stat-line" v-for="s in expense" :key="`e-${s.tagName}`">
            <span>{{ s.tagName }}</span>
            <span>{{ s.count }} 笔 · {{ s.totalAmount.toFixed(2) }} 元</span>
          </div>
        </template>
      </van-cell>
      <van-cell v-if="income.length === 0 && expense.length === 0" title="本月暂无标签统计" />
    </van-cell-group>

    <!-- 标签列表 -->
    <van-cell-group inset class="list">
      <van-cell v-for="t in tags" :key="t.id" :title="t.name" :label="`创建于 ${t.createdAt}`">
        <template #right-icon>
          <van-icon name="delete-o" size="18" color="#B3541E" class="del" @click="onDelete(t)" />
        </template>
      </van-cell>
      <van-empty v-if="tags.length === 0" description="还没有标签，点右上角 + 新建" image-size="80" />
    </van-cell-group>

    <!-- 新建标签弹窗 -->
    <van-dialog v-model:show="showCreate" title="新建标签" show-cancel-button @confirm="onCreate">
      <van-field v-model="newName" placeholder="标签名（最长 50 字）" maxlength="50" />
    </van-dialog>
  </div>
</template>

<style scoped>
.stats { margin-top: 12px; }
.month-label { font-weight: 600; font-size: 14px; }
.arrow-next { margin-left: 12px; }
.stat-line { display: flex; justify-content: space-between; font-size: 13px; color: #666; }
.del { padding: 4px 0 0 8px; }
.list { margin-top: 12px; }
</style>