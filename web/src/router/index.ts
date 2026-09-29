import { createRouter, createWebHashHistory } from 'vue-router'
import { auth } from '../store/auth'

const routes = [
  { path: '/login', component: () => import('../views/LoginView.vue') },
  { path: '/', component: () => import('../views/InputView.vue') },
  { path: '/ledger', component: () => import('../views/LedgerView.vue') },
  { path: '/schedule', component: () => import('../views/ScheduleView.vue') },
  { path: '/knowledge', component: () => import('../views/KnowledgeView.vue') },
  { path: '/tags', component: () => import('../views/TagsView.vue') },
  { path: '/ask', component: () => import('../views/AskView.vue') },
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

export const router = createRouter({
  history: createWebHashHistory(),
  routes,
})

// 登录守卫
router.beforeEach((to) => {
  if (to.path !== '/login' && !auth.token) return '/login'
  if (to.path === '/login' && auth.token) return '/'
  return true
})