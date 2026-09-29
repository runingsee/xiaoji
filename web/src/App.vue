<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'
import { onMounted, ref } from 'vue'

const route = useRoute()
const router = useRouter()
const showTab = ref(false)

onMounted(() => {
  showTab.value = route.path !== '/login'
})
</script>

<template>
  <div class="page-shell">
    <router-view />

    <van-tabbar v-if="showTab" :model-value="route.path" route active-color="#B3541E" inactive-color="#8C8C8C">
      <van-tabbar-item replace to="/" icon="edit">口述</van-tabbar-item>
      <van-tabbar-item replace to="/ledger" icon="balance-list-o">账本</van-tabbar-item>
      <van-tabbar-item replace to="/schedule" icon="clock-o">日程</van-tabbar-item>
      <van-tabbar-item replace to="/knowledge" icon="notes-o">知识</van-tabbar-item>
      <van-tabbar-item replace to="/tags" icon="tag-o">标签</van-tabbar-item>
    </van-tabbar>
  </div>
</template>

<style>
:root {
  --van-primary-color: #ffc940;
  --van-tabbar-item-active-background: #fff8ec;
  --van-background-color: #fff8ec;
}
* { box-sizing: border-box; }
body {
  margin: 0;
  background: #fff8ec;
  font-family: -apple-system, 'PingFang SC', 'Microsoft YaHei', sans-serif;
  -webkit-tap-highlight-color: transparent;
}
.page-shell {
  min-height: 100vh;
  padding-bottom: 60px; /* 给 Tabbar 让位 */
}
.van-nav-bar { background: #fff8ec; }
</style>